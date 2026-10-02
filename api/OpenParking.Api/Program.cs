using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenParking.Api.Hubs;
using OpenParking.Api.Middleware;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Seeders;
using OpenParking.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
DotNetEnv.Env.TraversePath().Load();
builder.Configuration.AddEnvironmentVariables();

// ── Logging (structured, easy grep in prod) ──────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole(opts => opts.FormatterName = "simple");
if (builder.Environment.IsDevelopment())
    builder.Logging.AddDebug();

// ── MVC / SignalR ────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
var signalRBuilder = builder.Services.AddSignalR();
var redisUrl = builder.Configuration["REDIS_URL"];
if (!string.IsNullOrEmpty(redisUrl))
{
    signalRBuilder.AddStackExchangeRedis(redisUrl);
}
builder.Services.AddMemoryCache();

// ── Database ─────────────────────────────────────────────────────────────
var connectionString = builder.Configuration["DATABASE_URL"] ?? builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string is not set. Add DATABASE_URL to .env or ConnectionStrings:Default to appsettings.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    // Log all SQL in development — invaluable for debugging queries
    if (builder.Environment.IsDevelopment())
        options.EnableSensitiveDataLogging().EnableDetailedErrors();
});

// ── JWT Authentication ────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["JWT:Secret"]
    ?? throw new InvalidOperationException(
        "JWT:Secret is not configured. Add it to appsettings.json or the JWT_SECRET environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Push JWT auth errors into the structured error response
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(
                    """{"success":false,"data":null,"error":{"code":"UNAUTHORIZED","message":"Authentication required. Provide a valid Bearer token."}}""");
            },
            OnForbidden = ctx =>
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(
                    """{"success":false,"data":null,"error":{"code":"FORBIDDEN","message":"You do not have permission to perform this action."}}""");
            }
        };
    });

builder.Services.AddAuthorization();

// ── Domain Services ───────────────────────────────────────────────────────

// Shared infrastructure
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddHttpClient<IEmailService, EmailService>(client =>
{
    client.DefaultRequestHeaders.Add("Authorization",
        $"Bearer {builder.Configuration["RESEND_API_KEY"] ?? ""}");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Module 1 — User & Access (Yowun)
builder.Services.AddScoped<IUserService, UserService>();

// Module 2 — Space & Availability (Supun)
builder.Services.AddScoped<IZoneService, ZoneService>();

// Module 3 — Booking & Payment (Dev)
builder.Services.AddScoped<IBookingService, BookingService>();

// Module 4 — Enforcement & AI Orchestration (Karuna)
// Uses a named HttpClient for LangGraph calls (30s timeout for AI inference)
builder.Services.AddHttpClient<IEnforcementService, EnforcementService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
// SignalR notification abstraction (decouples Infrastructure from the Hub type)
builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();

// Cross-cutting Services
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// Register modules as IParkingModule for HealthController discovery
builder.Services.AddScoped<IParkingModule>(sp => sp.GetRequiredService<IUserService>());
builder.Services.AddScoped<IParkingModule>(sp => sp.GetRequiredService<IZoneService>());
builder.Services.AddScoped<IParkingModule>(sp => sp.GetRequiredService<IBookingService>());
builder.Services.AddScoped<IParkingModule>(sp => sp.GetRequiredService<IEnforcementService>());

// ── Background Services ───────────────────────────────────────────────────
// Overstay detection loop (design.md §8.3 step 1) — runs every N minutes
builder.Services.AddHostedService<OpenParking.Infrastructure.Services.OverstayDetectionService>();

// ── CORS ──────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    // Dev: allow all origins (Vite / Flutter dev servers)
    options.AddPolicy("DevAllowAll", p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    // Prod: restrict to Cloudflare Pages origin
    var cfPagesOrigin = builder.Configuration["AllowedOrigins:CloudflarePages"] ?? "";
    options.AddPolicy("Production", p =>
        p.WithOrigins(cfPagesOrigin).AllowAnyMethod().AllowAnyHeader().AllowCredentials());
});

// ── Swagger with JWT support ──────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpenParking API",
        Version = "v1",
        Description = "OpenParking — AI-driven parking management system. " +
                      "Use the Authorize button to supply a Bearer token for protected endpoints."
    });

    // Wire up JWT auth in Swagger UI
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token (without the 'Bearer' prefix)."
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// Global error handler MUST be first in the pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger always on — gate it behind CF Access in production, not code
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenParking v1");
    c.RoutePrefix = "swagger"; // accessible at /swagger
    c.DisplayRequestDuration(); // shows ms per request — helpful for debugging
});

app.UseCors(app.Environment.IsDevelopment() ? "DevAllowAll" : "Production");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SlotHub>("/hubs/slots");  // IHubContext<ISlotHub> is now available via DI

// Health probe (used by Docker and CF Tunnel health checks)
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", utc = DateTime.UtcNow }))
   .AllowAnonymous();

// Fast exit for CI swagger-check mode
if (args.Contains("--swagger-check"))
{
    app.Logger.LogInformation("Swagger check completed successfully");
    return;
}

// ── Startup: migrate + seed ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
        await SystemSettingsSeeder.SeedAsync(db);
        await UserSeeder.SeedAsync(db);
        await ZoneSeeder.SeedAsync(db);
        logger.LogInformation("Database migration and seeding completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Startup database migration/seed failed. " +
            "Check the connection string and that PostgreSQL is running.");
        // Don't swallow — crash fast so the problem is visible immediately
        throw;
    }
}

app.Run();
