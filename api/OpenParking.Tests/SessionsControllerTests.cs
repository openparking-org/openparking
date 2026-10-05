using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OpenParking.Api.Controllers;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using Xunit;

namespace OpenParking.Tests;

public class SessionsControllerTests
{
    private static SessionsController Controller(IBookingService service, Guid callerId, string role = "Driver") =>
        new(service, Mock.Of<ISettingsService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, callerId.ToString()),
                        new Claim(ClaimTypes.Role, role)
                    }, "test"))
                }
            }
        };

    [Fact]
    public void AllSessionEndpoints_RequireAuthentication() =>
        Assert.NotEmpty(typeof(SessionsController).GetCustomAttributes(typeof(AuthorizeAttribute), true));

    [Theory]
    [InlineData("read")]
    [InlineData("active")]
    [InlineData("checkout")]
    public async Task OtherUsersSession_IsForbidden(string operation)
    {
        var session = new ParkingSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var service = new Mock<IBookingService>();
        service.Setup(s => s.GetSessionAsync(session.Id)).ReturnsAsync(session);
        service.Setup(s => s.GetActiveSessionAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>())).ReturnsAsync(session);
        var controller = Controller(service.Object, Guid.NewGuid());
        var error = await Assert.ThrowsAsync<AppException>(async () =>
        {
            if (operation == "read") await controller.GetSessionById(session.Id);
            else if (operation == "active") await controller.GetActiveSession(bookingId: Guid.NewGuid());
            else await controller.CheckOut(new CheckOutRequest { SessionId = session.Id });
        });
        Assert.Equal(ErrorCodes.Forbidden, error.Code);
        service.Verify(s => s.CheckOutAsync(It.IsAny<CheckOutRequest>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CheckIn_UsesAuthenticatedUserInsteadOfBodyUserId()
    {
        var callerId = Guid.NewGuid();
        var service = new Mock<IBookingService>();
        service.Setup(s => s.CheckInAsync(It.IsAny<CheckInRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new ParkingSession { UserId = callerId });
        var controller = Controller(service.Object, callerId);
        await controller.CheckIn(new CheckInRequest { UserId = Guid.NewGuid(), SlotId = Guid.NewGuid() });
        service.Verify(s => s.CheckInAsync(It.Is<CheckInRequest>(r => r.UserId == callerId), It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData("ParkingAdmin")]
    [InlineData("SystemAdmin")]
    public async Task Admin_CanReadOtherUsersSession(string role)
    {
        var session = new ParkingSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var service = new Mock<IBookingService>();
        service.Setup(s => s.GetSessionAsync(session.Id)).ReturnsAsync(session);
        var result = await Controller(service.Object, Guid.NewGuid(), role).GetSessionById(session.Id);
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
