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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SystemSetting configuration
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(64);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Zone & Slot relations
        modelBuilder.Entity<Zone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
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

        // Booking & Sessions
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Slot).WithMany().HasForeignKey(e => e.SlotId);
        });

        modelBuilder.Entity<ParkingSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId);
        });

        // Permits & Workflow Runs
        modelBuilder.Entity<DisabilityPermit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<AgentWorkflowRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
        });
    }
}
