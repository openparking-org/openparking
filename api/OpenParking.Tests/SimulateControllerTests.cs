using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Moq;
using OpenParking.Api.Controllers;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Entities;
using Xunit;
using OpenParking.Core.Models;
using System;
using System.Threading.Tasks;

namespace OpenParking.Tests;

public class SimulateControllerTests
{
    private readonly Mock<IBookingService> _bookingServiceMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly SimulateController _controller;

    public SimulateControllerTests()
    {
        _bookingServiceMock = new Mock<IBookingService>();
        _configMock = new Mock<IConfiguration>();

        _controller = new SimulateController(_bookingServiceMock.Object, _configMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task SimulateEntry_ReturnsUnauthorized_WhenApiKeyIsMissing()
    {
        // Arrange
        _configMock.Setup(c => c["SIMULATION_API_KEY"]).Returns("secret-key");
        // No header set

        var req = new SimulateEntryRequest { LicensePlate = "WP-1234", ZoneCode = "ZONE-A" };

        // Act
        var result = await _controller.SimulateEntry(req);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SimulateEntry_ReturnsUnauthorized_WhenApiKeyIsIncorrect()
    {
        // Arrange
        _configMock.Setup(c => c["SIMULATION_API_KEY"]).Returns("secret-key");
        _controller.Request.Headers["X-Api-Key"] = "wrong-key";

        var req = new SimulateEntryRequest { LicensePlate = "WP-1234", ZoneCode = "ZONE-A" };

        // Act
        var result = await _controller.SimulateEntry(req);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SimulateEntry_ReturnsOk_WhenApiKeyIsCorrect()
    {
        // Arrange
        _configMock.Setup(c => c["SIMULATION_API_KEY"]).Returns("secret-key");
        _controller.Request.Headers["X-Api-Key"] = "secret-key";

        var session = new ParkingSession { Id = Guid.NewGuid() };
        _bookingServiceMock.Setup(b => b.SimulateEntryAsync("WP-1234", "ZONE-A")).ReturnsAsync(session);

        var req = new SimulateEntryRequest { LicensePlate = "WP-1234", ZoneCode = "ZONE-A" };

        // Act
        var result = await _controller.SimulateEntry(req);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SimulateExit_ReturnsUnauthorized_WhenApiKeyIsMissing()
    {
        // Arrange
        _configMock.Setup(c => c["SIMULATION_API_KEY"]).Returns("secret-key");
        
        var req = new SimulateExitRequest { LicensePlate = "WP-1234" };

        // Act
        var result = await _controller.SimulateExit(req);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SimulateExit_ReturnsOk_WhenApiKeyIsCorrect()
    {
        // Arrange
        _configMock.Setup(c => c["SIMULATION_API_KEY"]).Returns("secret-key");
        _controller.Request.Headers["X-Api-Key"] = "secret-key";

        var checkOutResult = new SessionCheckOutResult { SessionId = Guid.NewGuid() };
        _bookingServiceMock.Setup(b => b.SimulateExitAsync("WP-1234")).ReturnsAsync(checkOutResult);

        var req = new SimulateExitRequest { LicensePlate = "WP-1234" };

        // Act
        var result = await _controller.SimulateExit(req);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
