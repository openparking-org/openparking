using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;

namespace OpenParking.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<FloorPlan> FloorPlans => Set<FloorPlan>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ParkingSession> ParkingSessions => Set<ParkingSession>();
    public DbSet<DisabilityPermit> DisabilityPermits => Set<DisabilityPermit>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AgentWorkflowRun> AgentWorkflowRuns => Set<AgentWorkflowRun>();
    /// <summary>Active AI-approved surge pricing rules per zone. (Issue #24 / design.md §19.1)</summary>
    public DbSet<ZonePricingRule> ZonePricingRules => Set<ZonePricingRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ------------------------------------------------------------------
        // SystemSetting
        // ------------------------------------------------------------------
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(64);
        });

        // ------------------------------------------------------------------
        // User
        // ------------------------------------------------------------------
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // ------------------------------------------------------------------
        // Zone & Slot relations
        // ------------------------------------------------------------------
        modelBuilder.Entity<Zone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.BaseHourlyRate).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Slot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Zone)
                  .WithMany(z => z.Slots)
                  .HasForeignKey(e => e.ZoneId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FloorPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Zone)
                  .WithMany(z => z.FloorPlans)
                  .HasForeignKey(e => e.ZoneId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------------------------------------------------------
        // Booking & Sessions
        // ------------------------------------------------------------------
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Slot).WithMany().HasForeignKey(e => e.SlotId);
            entity.Property(e => e.EstimatedFee).HasPrecision(10, 2);
        });

        modelBuilder.Entity<ParkingSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId);
            entity.Property(e => e.TotalFee).HasPrecision(10, 2);
            entity.Property(e => e.PenaltyFee).HasPrecision(10, 2);
            // Indexes for active-session lookups
            entity.HasIndex(e => e.SlotId);
            entity.HasIndex(e => e.Status);
        });

        // ------------------------------------------------------------------
        // DisabilityPermit
        // ------------------------------------------------------------------
        modelBuilder.Entity<DisabilityPermit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // ------------------------------------------------------------------
        // AgentWorkflowRun — Issue #24 additions
        // ------------------------------------------------------------------
        modelBuilder.Entity<AgentWorkflowRun>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Indexes for pending-approval list and analytics filtering
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.WorkflowType);
            entity.HasIndex(e => e.TriggeredAt);

            // Constrained text columns
            entity.Property(e => e.Objective).IsRequired().HasMaxLength(500);
            entity.Property(e => e.WorkflowType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
            entity.Property(e => e.CurrentStep).HasMaxLength(50);
            entity.Property(e => e.DecisionReason).HasMaxLength(1000);
            entity.Property(e => e.ResolvedBy).HasMaxLength(256);

            // JSONB columns (PostgreSQL native JSON storage with indexing support)
            entity.Property(e => e.PlanJson)
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'{}'");
            entity.Property(e => e.StepResultsJson)
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'{}'");
            entity.Property(e => e.InputPayloadJson)
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'{}'");
            entity.Property(e => e.ExecutionSummaryJson)
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'{}'");
            entity.Property(e => e.ErrorLogJson)
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'{}'");

            // FK to User (nullable — no cascade; audit runs survive user deletion)
            entity.HasOne(e => e.ApprovedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ApprovedBy)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ------------------------------------------------------------------
        // ZonePricingRule — Issue #24 new table (design.md §19.1)
        // ------------------------------------------------------------------
        modelBuilder.Entity<ZonePricingRule>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Multiplier).HasPrecision(4, 2);
            entity.Property(e => e.Reason).HasMaxLength(500);

            // Composite index: find active rule for a zone at a timestamp
            entity.HasIndex(e => new { e.ZoneId, e.ActiveFrom });

            // Zone FK — restrict so zones with active rules cannot be silently deleted
            entity.HasOne(e => e.Zone)
                  .WithMany()
                  .HasForeignKey(e => e.ZoneId)
                  .OnDelete(DeleteBehavior.Restrict);

            // ApprovedBy User FK (nullable)
            entity.HasOne(e => e.ApprovedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ApprovedBy)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            // WorkflowRun FK (nullable — set null if run is deleted)
            entity.HasOne(e => e.WorkflowRun)
                  .WithMany()
                  .HasForeignKey(e => e.WorkflowRunId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

