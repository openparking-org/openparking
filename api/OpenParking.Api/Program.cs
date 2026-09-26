using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Hubs;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Seeders;
using OpenParking.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

// JWT Authentication & Authorization
var jwtSecret = builder.Configuration["JWT:Secret"] 
    ?? "development-super-secret-key-that-is-at-least-32-chars-long";

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("Default") 
    ?? "Host=localhost;Port=5432;Database=openparking;Username=dev;Password=dev";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register domain services
builder.Services.AddScoped<ISettingsService, SettingsService>();

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Swagger for API exploration
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SlotHub>("/hubs/slots");

// Check if running only for swagger check
if (args.Contains("--swagger-check"))
{
    app.Logger.LogInformation("Swagger check completed successfully");
    return;
}

// Auto-seed system settings on startup if DB is accessible
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.CanConnect())
    {
        await SystemSettingsSeeder.SeedAsync(db);
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning("Database connection during startup skipped: {Message}", ex.Message);
}

app.Run();
