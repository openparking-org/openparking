using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenParking.Api.Controllers;
using OpenParking.Core.Entities;
using OpenParking.Infrastructure.Data;
using Xunit;

namespace OpenParking.Tests;

public class WorkflowApprovalTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static WorkflowsController CreateController(AppDbContext db, Guid? userId = null, string? email = null, string role = "ParkingAdmin")
    {
        var controller = new WorkflowsController(db);
        var claims = new List<Claim>();

        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            claims.Add(new Claim("sub", userId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
            claims.Add(new Claim(ClaimTypes.Name, email));
        }

        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        return controller;
    }

    [Fact]
    public async Task GetWorkflowById_Returns_Ok_For_Existing_Workflow()
    {
        using var db = CreateInMemoryDb();
        var run = new AgentWorkflowRun
        {
            Objective = "Test Overstay Enforcement",
            WorkflowType = "OVERSTAY_ENFORCEMENT",
            Status = "PENDING_APPROVAL"
        };
        db.AgentWorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetWorkflowById(run.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<WorkflowResponseDto>(okResult.Value);
        Assert.Equal(run.Id, dto.Id);
        Assert.Equal("PENDING_APPROVAL", dto.Status);
        Assert.Equal("Test Overstay Enforcement", dto.Objective);
    }

    [Fact]
    public async Task GetWorkflowById_Returns_NotFound_For_Unknown_Id()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.GetWorkflowById(Guid.NewGuid());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ApproveWorkflow_Transitions_Pending_Workflow_To_Approved()
    {
        using var db = CreateInMemoryDb();
        var adminId = Guid.NewGuid();
        var adminEmail = "admin@openparking.org";

        var run = new AgentWorkflowRun
        {
            Objective = "Approve Surge Multiplier",
            WorkflowType = "DYNAMIC_PRICING",
            Status = "PENDING_APPROVAL"
        };
        db.AgentWorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        var controller = CreateController(db, userId: adminId, email: adminEmail, role: "ParkingAdmin");
        var result = await controller.ApproveWorkflow(run.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify database state
        var updated = await db.AgentWorkflowRuns.FindAsync(run.Id);
        Assert.NotNull(updated);
        Assert.Equal("APPROVED", updated.Status);
        Assert.Equal(adminId, updated.ApprovedBy);
        Assert.Equal(adminEmail, updated.ResolvedBy);
        Assert.NotNull(updated.ResolvedAt);
        Assert.True((DateTime.UtcNow - updated.ResolvedAt.Value).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - updated.UpdatedAt).TotalSeconds < 5);
    }

    [Fact]
    public async Task ApproveWorkflow_Rejects_Already_Approved_Workflow()
    {
        using var db = CreateInMemoryDb();
        var run = new AgentWorkflowRun
        {
            Objective = "Already Approved Workflow",
            WorkflowType = "DYNAMIC_PRICING",
            Status = "APPROVED"
        };
        db.AgentWorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.ApproveWorkflow(run.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("cannot be approved", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task RejectWorkflow_Transitions_Pending_Workflow_To_Rejected_With_Reason()
    {
        using var db = CreateInMemoryDb();
        var adminId = Guid.NewGuid();
        var adminEmail = "systemadmin@openparking.org";

        var run = new AgentWorkflowRun
        {
            Objective = "Invalid Penalty Proposal",
            WorkflowType = "OVERSTAY_ENFORCEMENT",
            Status = "PENDING_APPROVAL"
        };
        db.AgentWorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        var controller = CreateController(db, userId: adminId, email: adminEmail, role: "SystemAdmin");
        var req = new RejectWorkflowRequest { Reason = "User had valid medical emergency extension" };
        var result = await controller.RejectWorkflow(run.Id, req);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var updated = await db.AgentWorkflowRuns.FindAsync(run.Id);
        Assert.NotNull(updated);
        Assert.Equal("REJECTED", updated.Status);
        Assert.Equal("User had valid medical emergency extension", updated.DecisionReason);
        Assert.Equal(adminId, updated.ApprovedBy);
        Assert.Equal(adminEmail, updated.ResolvedBy);
        Assert.NotNull(updated.ResolvedAt);
    }

    [Fact]
    public async Task RejectWorkflow_Rejects_Invalid_Workflow_State()
    {
        using var db = CreateInMemoryDb();
        var run = new AgentWorkflowRun
        {
            Objective = "Already Rejected Workflow",
            WorkflowType = "OVERSTAY_ENFORCEMENT",
            Status = "REJECTED"
        };
        db.AgentWorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.RejectWorkflow(run.Id, new RejectWorkflowRequest { Reason = "Duplicate" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("cannot be rejected", badRequest.Value?.ToString());
    }

    [Fact]
    public void WorkflowsController_Is_Protected_With_Admin_Roles()
    {
        var controllerType = typeof(WorkflowsController);
        var authAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttribute);
        Assert.NotNull(authAttribute.Roles);
        Assert.Contains("ParkingAdmin", authAttribute.Roles);
        Assert.Contains("SystemAdmin", authAttribute.Roles);
    }

    [Fact]
    public async Task GetWorkflows_Filters_By_Status()
    {
        using var db = CreateInMemoryDb();
        db.AgentWorkflowRuns.AddRange(
            new AgentWorkflowRun { Objective = "Run 1", Status = "PENDING_APPROVAL" },
            new AgentWorkflowRun { Objective = "Run 2", Status = "APPROVED" },
            new AgentWorkflowRun { Objective = "Run 3", Status = "PENDING_APPROVAL" }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetWorkflows(status: "PENDING_APPROVAL");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<WorkflowResponseDto>>(okResult.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public async Task GetPendingWorkflows_Returns_Only_Pending_Or_Awaiting()
    {
        using var db = CreateInMemoryDb();
        db.AgentWorkflowRuns.AddRange(
            new AgentWorkflowRun { Objective = "Run 1", Status = "PENDING_APPROVAL" },
            new AgentWorkflowRun { Objective = "Run 2", Status = "AWAITING_APPROVAL" },
            new AgentWorkflowRun { Objective = "Run 3", Status = "APPROVED" },
            new AgentWorkflowRun { Objective = "Run 4", Status = "REJECTED" }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetPendingWorkflows();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<WorkflowResponseDto>>(okResult.Value);
        Assert.Equal(2, list.Count());
    }
}
