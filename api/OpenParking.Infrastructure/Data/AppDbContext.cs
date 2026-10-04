using Microsoft.EntityFrameworkCore;
using OpenParking.Core.Entities;

namespace OpenParking.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // ── DbSets ────────────────────────────────────────────────────────────
    public DbSet<User>              Users             => Set<User>();
    public DbSet<Zone>              Zones             => Set<Zone>();
    public DbSet<Slot>              Slots             => Set<Slot>();
    public DbSet<FloorPlan>         FloorPlans        => Set<FloorPlan>();
    public DbSet<Booking>           Bookings          => Set<Booking>();
    public DbSet<ParkingSession>    ParkingSessions   => Set<ParkingSession>();
    public DbSet<DisabilityPermit>  DisabilityPermits => Set<DisabilityPermit>();
    public DbSet<SystemSetting>     SystemSettings    => Set<SystemSetting>();
    public DbSet<AgentWorkflowRun>  AgentWorkflowRuns => Set<AgentWorkflowRun>();
    public DbSet<Penalty>           Penalties         => Set<Penalty>();
    public DbSet<AuditLog>          AuditLogs         => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── SystemSetting ─────────────────────────────────────────────────
        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.Value).IsRequired();
            e.Property(x => x.Category).HasMaxLength(64);
        });

        // ── User ──────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(256);
        });

        // ── Zone ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Zone>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(16);
        });

        // ── Slot ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Slot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).IsConcurrencyToken();
            // Filter index: quickly find all available slots in a zone
            e.HasIndex(x => new { x.ZoneId, x.Status });
            e.HasOne(x => x.Zone)
             .WithMany(z => z.Slots)
             .HasForeignKey(x => x.ZoneId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── FloorPlan ─────────────────────────────────────────────────────
        modelBuilder.Entity<FloorPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Zone)
             .WithMany(z => z.FloorPlans)
             .HasForeignKey(x => x.ZoneId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Booking ───────────────────────────────────────────────────────
        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(x => x.Id);
            // Index on status + date range for overstay background service queries
            e.HasIndex(x => new { x.Status, x.StartTime });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Slot).WithMany().HasForeignKey(x => x.SlotId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── ParkingSession ────────────────────────────────────────────────
        modelBuilder.Entity<ParkingSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Status, x.CheckInTime });
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Slot).WithMany().HasForeignKey(x => x.SlotId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // ── DisabilityPermit ──────────────────────────────────────────────
        modelBuilder.Entity<DisabilityPermit>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── AgentWorkflowRun ──────────────────────────────────────────────
        modelBuilder.Entity<AgentWorkflowRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.WorkflowType);
            e.Property(x => x.Status).HasConversion<string>().IsConcurrencyToken();
            // Optional FK to the session that triggered this workflow
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId)
             .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // ── Penalty ───────────────────────────────────────────────────────
        modelBuilder.Entity<Penalty>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.WorkflowRun).WithMany().HasForeignKey(x => x.WorkflowRunId).IsRequired(false);
            e.Property(x => x.Amount).HasPrecision(10, 2);
        });

        // ── AuditLog ─────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.CreatedAt);  // renamed from OccurredAt
            // Audit logs are immutable — disable cascade updates
            e.ToTable("audit_logs");
        });
    }
}
