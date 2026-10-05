using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenParking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflowRuns_Users_ApprovedBy",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Slots_SlotId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_UserId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSessions_Bookings_BookingId",
                table: "ParkingSessions");

            migrationBuilder.DropTable(
                name: "ZonePricingRules");

            migrationBuilder.DropIndex(
                name: "IX_Slots_ZoneId",
                table: "Slots");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSessions_Status",
                table: "ParkingSessions");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflowRuns_ApprovedBy",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflowRuns_TriggeredAt",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "CurrentStep",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ErrorLogJson",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ExecutionSummaryJson",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "InputPayloadJson",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ResolvedBy",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggeredAt",
                table: "AgentWorkflowRuns");

            migrationBuilder.RenameColumn(
                name: "SubmittedAt",
                table: "DisabilityPermits",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ResolvedAt",
                table: "AgentWorkflowRuns",
                newName: "ApprovedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Zones",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseHourlyRate",
                table: "Zones",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Zones",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Slots",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Slots",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalFee",
                table: "ParkingSessions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "PenaltyFee",
                table: "ParkingSessions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "FloorPlans",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "DisabilityPermits",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "DisabilityPermits",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedFee",
                table: "Bookings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Bookings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "VehiclePlate",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowType",
                table: "AgentWorkflowRuns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "StepResultsJson",
                table: "AgentWorkflowRuns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'{}'");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AgentWorkflowRuns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "PlanJson",
                table: "AgentWorkflowRuns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'{}'");

            migrationBuilder.AlterColumn<string>(
                name: "Objective",
                table: "AgentWorkflowRuns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "DecisionReason",
                table: "AgentWorkflowRuns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<Guid>(
                name: "ApproverUserId",
                table: "AgentWorkflowRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AgentWorkflowRuns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorLog",
                table: "AgentWorkflowRuns",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorEmail = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Penalties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DisputeNotes = table.Column<string>(type: "text", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Penalties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Penalties_AgentWorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "AgentWorkflowRuns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Penalties_ParkingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ParkingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Penalties_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Slots_FloorPlanId",
                table: "Slots",
                column: "FloorPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Slots_ZoneId_Status",
                table: "Slots",
                columns: new[] { "ZoneId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_Status_CheckInTime",
                table: "ParkingSessions",
                columns: new[] { "Status", "CheckInTime" });

            migrationBuilder.CreateIndex(
                name: "IX_DisabilityPermits_Status",
                table: "DisabilityPermits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_StartTime",
                table: "Bookings",
                columns: new[] { "Status", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_ApproverUserId",
                table: "AgentWorkflowRuns",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityType_EntityId",
                table: "audit_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Penalties_SessionId",
                table: "Penalties",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Penalties_Status",
                table: "Penalties",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Penalties_UserId",
                table: "Penalties",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Penalties_WorkflowRunId",
                table: "Penalties",
                column: "WorkflowRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflowRuns_ParkingSessions_SessionId",
                table: "AgentWorkflowRuns",
                column: "SessionId",
                principalTable: "ParkingSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflowRuns_Users_ApproverUserId",
                table: "AgentWorkflowRuns",
                column: "ApproverUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Slots_SlotId",
                table: "Bookings",
                column: "SlotId",
                principalTable: "Slots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_UserId",
                table: "Bookings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSessions_Bookings_BookingId",
                table: "ParkingSessions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSessions_Slots_SlotId",
                table: "ParkingSessions",
                column: "SlotId",
                principalTable: "Slots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Slots_FloorPlans_FloorPlanId",
                table: "Slots",
                column: "FloorPlanId",
                principalTable: "FloorPlans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflowRuns_ParkingSessions_SessionId",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflowRuns_Users_ApproverUserId",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Slots_SlotId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_UserId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSessions_Bookings_BookingId",
                table: "ParkingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSessions_Slots_SlotId",
                table: "ParkingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Slots_FloorPlans_FloorPlanId",
                table: "Slots");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "Penalties");

            migrationBuilder.DropIndex(
                name: "IX_Slots_FloorPlanId",
                table: "Slots");

            migrationBuilder.DropIndex(
                name: "IX_Slots_ZoneId_Status",
                table: "Slots");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSessions_Status_CheckInTime",
                table: "ParkingSessions");

            migrationBuilder.DropIndex(
                name: "IX_DisabilityPermits_Status",
                table: "DisabilityPermits");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_Status_StartTime",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflowRuns_ApproverUserId",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FloorPlans");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DisabilityPermits");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "DisabilityPermits");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "VehiclePlate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ApproverUserId",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ErrorLog",
                table: "AgentWorkflowRuns");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "DisabilityPermits",
                newName: "SubmittedAt");

            migrationBuilder.RenameColumn(
                name: "ApprovedAt",
                table: "AgentWorkflowRuns",
                newName: "ResolvedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Zones",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<decimal>(
                name: "BaseHourlyRate",
                table: "Zones",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalFee",
                table: "ParkingSessions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "PenaltyFee",
                table: "ParkingSessions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedFee",
                table: "Bookings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowType",
                table: "AgentWorkflowRuns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "StepResultsJson",
                table: "AgentWorkflowRuns",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AgentWorkflowRuns",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PlanJson",
                table: "AgentWorkflowRuns",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Objective",
                table: "AgentWorkflowRuns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DecisionReason",
                table: "AgentWorkflowRuns",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "CurrentStep",
                table: "AgentWorkflowRuns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorLogJson",
                table: "AgentWorkflowRuns",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "ExecutionSummaryJson",
                table: "AgentWorkflowRuns",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "InputPayloadJson",
                table: "AgentWorkflowRuns",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "ResolvedBy",
                table: "AgentWorkflowRuns",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TriggeredAt",
                table: "AgentWorkflowRuns",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "ZonePricingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActiveUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Multiplier = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZonePricingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZonePricingRules_AgentWorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "AgentWorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ZonePricingRules_Users_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ZonePricingRules_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Slots_ZoneId",
                table: "Slots",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_Status",
                table: "ParkingSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_ApprovedBy",
                table: "AgentWorkflowRuns",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_TriggeredAt",
                table: "AgentWorkflowRuns",
                column: "TriggeredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ZonePricingRules_ApprovedBy",
                table: "ZonePricingRules",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ZonePricingRules_WorkflowRunId",
                table: "ZonePricingRules",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ZonePricingRules_ZoneId_ActiveFrom",
                table: "ZonePricingRules",
                columns: new[] { "ZoneId", "ActiveFrom" });

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflowRuns_Users_ApprovedBy",
                table: "AgentWorkflowRuns",
                column: "ApprovedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Slots_SlotId",
                table: "Bookings",
                column: "SlotId",
                principalTable: "Slots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_UserId",
                table: "Bookings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSessions_Bookings_BookingId",
                table: "ParkingSessions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
