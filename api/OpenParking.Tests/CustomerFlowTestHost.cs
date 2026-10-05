using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenParking.Api.Controllers;
using OpenParking.Api.Middleware;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;

namespace OpenParking.Tests;

/// <summary>Local-only verification host. All data is disposable; email and AI are isolated.</summary>
public static class CustomerFlowTestHost
{
    public static readonly Guid DriverId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ZoneId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid SlotId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    public static async Task<WebApplication> StartAsync(int port = 0, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Verification" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddControllers().AddApplicationPart(typeof(GateController).Assembly).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Services.AddAuthentication("Verification").AddScheme<AuthenticationSchemeOptions, VerificationAuthentication>("Verification", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddMemoryCache();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("http://127.0.0.1:5199").AllowAnyHeader().AllowAnyMethod()));
        builder.Services.AddSingleton(Mock.Of<IEmailService>()); builder.Services.AddSingleton(Mock.Of<IRealtimeNotifier>());
        var clients = new Mock<IHttpClientFactory>(); clients.Setup(c => c.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new IsolatedAiHandler()));
        builder.Services.AddSingleton(clients.Object);
        builder.Services.AddScoped<IBookingService, BookingService>(); builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ISettingsService, SettingsService>(); builder.Services.AddScoped<IZoneService, ZoneService>();
        builder.Services.AddScoped<IEnforcementService>(services => new EnforcementService(services.GetRequiredService<AppDbContext>(), new HttpClient(new IsolatedAiHandler()),
            services.GetRequiredService<IEmailService>(), services.GetRequiredService<IRealtimeNotifier>(), services.GetRequiredService<ISettingsService>(),
            builder.Configuration, services.GetRequiredService<ILogger<EnforcementService>>()));
        builder.Services.AddScoped<GateService>(); builder.Services.AddScoped<NavigationService>();
        configure?.Invoke(builder);
        var app = builder.Build(); app.UseCors(); app.UseMiddleware<GlobalExceptionMiddleware>(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(new User { Id = DriverId, FullName = "Verification Driver", Email = "driver@verification.test" },
                new User { Id = AdminId, FullName = "Verification Attendant", Email = "admin@verification.test", Role = UserRole.ParkingAdmin });
            var zone = new Zone { Id = ZoneId, Code = "VERIFY", Name = "Verification Parking", BaseHourlyRate = 100m, TotalCapacity = 1 };
            db.Zones.Add(zone); db.Slots.Add(new Slot { Id = SlotId, ZoneId = ZoneId, SlotNumber = "A1", Type = SlotType.Standard });
            db.SystemSettings.Add(new SystemSetting { Key = "pricing.default_currency", Value = "LKR", Category = "Pricing" });
            await db.SaveChangesAsync();
        }
        await app.StartAsync(); return app;
    }
    public static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

    private sealed class IsolatedAiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"multiplier\":1,\"confidence\":0}", System.Text.Encoding.UTF8, "application/json") });
    }
}

public class VerificationAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        var admin = header == "Bearer verification-admin";
        if (!admin && header != "Bearer verification-driver") return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, (admin ? CustomerFlowTestHost.AdminId : CustomerFlowTestHost.DriverId).ToString()),
            new Claim(ClaimTypes.Role, admin ? "ParkingAdmin" : "Driver")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
