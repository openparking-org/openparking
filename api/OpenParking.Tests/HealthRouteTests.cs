using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using OpenParking.Api.Controllers;
using Xunit;

namespace OpenParking.Tests;

public class HealthRouteTests
{
    [Fact]
    public void HealthController_DoesNotClaimTheLivenessProbePath()
    {
        // Program.cs maps GET /health as the liveness probe that Docker, the
        // Cloudflare Tunnel and deploy-vm.yml call. If the controller also
        // resolves to /health, routing is ambiguous and every probe returns 500.
        var route = typeof(HealthController).GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(route);
        Assert.Equal("health/modules", route!.Template);
    }
}
