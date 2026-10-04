using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;
using OpenParking.Infrastructure.Data;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

public class EnforcementIntegrationTests
{
    private static (AppDbContext db, Mock<HttpMessageHandler> httpMock, EnforcementService service) CreateService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        var emailMock = new Mock<IEmailService>();
        var notifierMock = new Mock<IRealtimeNotifier>();
        var settingsMock = new Mock<ISettingsService>();
        var loggerMock = new Mock<ILogger<EnforcementService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["AI_SERVICE_URL"]).Returns("http://ai-service.mock:8000");

        var httpMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var httpClient = new HttpClient(httpMock.Object);

        var service = new EnforcementService(
            db,
            httpClient,
            emailMock.Object,
            notifierMock.Object,
            settingsMock.Object,
            configMock.Object,
            loggerMock.Object);

        return (db, httpMock, service);
    }

    [Fact]
    public async Task TriggerWorkflow_UsesPythonExecutionContract_AndPersistsProposalForAdminReview()
    {
        var (db, httpMock, service) = CreateService();
        var booking = new Booking();
        var session = new ParkingSession { BookingId = booking.Id, Booking = booking, OverstayMinutes = 180 };
        db.AddRange(booking, session);
        await db.SaveChangesAsync();
        string? path = null;
        string? payload = null;
        httpMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken _) =>
            {
                path = request.RequestUri!.AbsolutePath;
                payload = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"PENDING_APPROVAL\",\"plan\":{\"steps\":[]},\"action_proposal\":{\"proposed_amount\":125,\"reason\":\"Overstay\"}}") };
            });
        var run = await service.TriggerWorkflowAsync("OVERSTAY", "Review overstay", session.Id, null);
        Assert.Equal("/workflows/execute", path);
        using var input = JsonDocument.Parse(payload!);
        Assert.Equal("OVERSTAY_ENFORCEMENT", input.RootElement.GetProperty("workflow_type").GetString());
        Assert.Equal(180, input.RootElement.GetProperty("input_data").GetProperty("overstay_minutes").GetInt32());
        Assert.Equal(WorkflowStatus.AwaitingApproval, run.Status);
        Assert.Contains("proposed_amount", run.StepResultsJson);
        Assert.Equal("{\"steps\":[]}", run.PlanJson);
    }

    [Fact]
    public async Task TriggerWorkflowAsync_WhenAiServiceReturnsSuccess_SetsStatusToRunning()
    {
        var (db, httpMock, service) = CreateService();
        var sessionId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        httpMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"started\"}")
            })
            .Verifiable();

        var run = await service.TriggerWorkflowAsync("OVERSTAY", "Test objective", sessionId, zoneId);

        Assert.NotNull(run);
        Assert.Equal(WorkflowStatus.Running, run.Status);
        Assert.Equal("OVERSTAY", run.WorkflowType);
        Assert.Equal(sessionId, run.SessionId);
        
        var inDb = await db.AgentWorkflowRuns.FindAsync(run.Id);
        Assert.NotNull(inDb);
        Assert.Equal(WorkflowStatus.Running, inDb.Status);
        
        httpMock.Verify();
    }

    [Fact]
    public async Task TriggerWorkflowAsync_WhenAiServiceReturns500_SetsStatusToFailedAndLogsError()
    {
        var (db, httpMock, service) = CreateService();

        httpMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal Server Error")
            })
            .Verifiable();

        var run = await service.TriggerWorkflowAsync("SURGE_PRICING", "Calculate surge", null, Guid.NewGuid());

        Assert.Equal(WorkflowStatus.Failed, run.Status);
        
        var inDb = await db.AgentWorkflowRuns.FindAsync(run.Id);
        Assert.NotNull(inDb);
        Assert.Equal(WorkflowStatus.Failed, inDb.Status);
        Assert.Contains("Internal Server Error", inDb.ErrorLog ?? "");
        Assert.Contains("500", inDb.ErrorLog ?? "");

        httpMock.Verify();
    }
    
    [Fact]
    public async Task TriggerWorkflowAsync_WhenAiServiceTimesOut_SetsStatusToFailedGracefully()
    {
        var (db, httpMock, service) = CreateService();

        httpMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException("A task was canceled."))
            .Verifiable();

        var run = await service.TriggerWorkflowAsync("OVERSTAY", "Timeout test", null, null);

        Assert.Equal(WorkflowStatus.Failed, run.Status);
        
        var inDb = await db.AgentWorkflowRuns.FindAsync(run.Id);
        Assert.NotNull(inDb);
        Assert.Equal(WorkflowStatus.Failed, inDb.Status);
        Assert.Contains("canceled", inDb.ErrorLog ?? "");

        httpMock.Verify();
    }
}
