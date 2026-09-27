using Microsoft.EntityFrameworkCore;
using Moq;
using OpenParking.Core.Entities;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace OpenParking.Tests;

public class UserServiceTests
{
    private readonly Mock<IConfiguration> _configMock;

    public UserServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["JWT:Secret"]).Returns("SuperSecretKeyThatIsAtLeast32BytesLong!");
        _configMock.Setup(c => c["JWT:Issuer"]).Returns("OpenParking");
        _configMock.Setup(c => c["JWT:Audience"]).Returns("OpenParkingUsers");
    }

    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var users = new List<User>
        {
            new User
            {
                Id = Guid.NewGuid(),
                Email = "driver@openparking.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = UserRole.Driver
            }
        };

        var dbContext = GetInMemoryDbContext();
        dbContext.Users.AddRange(users);
        await dbContext.SaveChangesAsync();

        var emailServiceMock = new Mock<OpenParking.Core.Interfaces.IEmailService>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<UserService>>();

        var userService = new UserService(dbContext, emailServiceMock.Object, _configMock.Object, loggerMock.Object);

        var result = await userService.LoginAsync("driver@openparking.local", "Password123!");

        Assert.NotNull(result.Token);
        Assert.Equal(users[0].Email, result.User.Email);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ThrowsAppException()
    {
        var users = new List<User>
        {
            new User
            {
                Id = Guid.NewGuid(),
                Email = "driver@openparking.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Role = UserRole.Driver
            }
        };

        var dbContext = GetInMemoryDbContext();
        dbContext.Users.AddRange(users);
        await dbContext.SaveChangesAsync();

        var emailServiceMock = new Mock<OpenParking.Core.Interfaces.IEmailService>();
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<UserService>>();

        var userService = new UserService(dbContext, emailServiceMock.Object, _configMock.Object, loggerMock.Object);

        await Assert.ThrowsAsync<AppException>(() => userService.LoginAsync("driver@openparking.local", "WrongPassword!"));
    }
}
