using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Tracking;
using FleetOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Infrastructure.Persistence;

public sealed class FleetOpsDbContext(DbContextOptions<FleetOpsDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionStop> MissionStops => Set<MissionStop>();
    public DbSet<MissionTimelineEvent> MissionTimelineEvents => Set<MissionTimelineEvent>();
    public DbSet<DriverSyncCommandReceipt> DriverSyncCommandReceipts => Set<DriverSyncCommandReceipt>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<GpsDevice> GpsDevices => Set<GpsDevice>();
    public DbSet<DeviceAssignment> DeviceAssignments => Set<DeviceAssignment>();
    public DbSet<TelemetryPoint> TelemetryPoints => Set<TelemetryPoint>();
    public DbSet<CurrentVehiclePosition> CurrentVehiclePositions => Set<CurrentVehiclePosition>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(160);
            entity.Property(x => x.Slug).HasMaxLength(80);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique()
                .HasFilter("[Email] IS NOT NULL");
            entity.HasOne<Driver>()
                .WithMany()
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
            entity.Property(x => x.ActionType).HasMaxLength(64);
            entity.Property(x => x.TargetType).HasMaxLength(64);
            entity.Property(x => x.TargetId).HasMaxLength(128);
            entity.Property(x => x.Metadata).HasMaxLength(2048);
        });

        builder.Entity<Mission>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.Reference }).IsUnique();
            entity.Property(x => x.Reference).HasMaxLength(48);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.ScheduledStartUtc).HasPrecision(7);
            entity.Property(x => x.ScheduledEndUtc).HasPrecision(7);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.Navigation(x => x.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.Timeline).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Stops)
                .WithOne()
                .HasForeignKey(x => x.MissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Timeline)
                .WithOne()
                .HasForeignKey(x => x.MissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MissionStop>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.Sequence }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Address).HasMaxLength(240);
            entity.Property(x => x.PlannedArrivalUtc).HasPrecision(7);
        });

        builder.Entity<MissionTimelineEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.MissionId, x.OccurredAtUtc });
            entity.Property(x => x.Description).HasMaxLength(240);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(7);
        });

        builder.Entity<DriverSyncCommandReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.CommandId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.DriverId, x.MissionId });
            entity.Property(x => x.CommandId).HasMaxLength(80);
            entity.Property(x => x.ProcessedAtUtc).HasPrecision(7);
        });

        builder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.RegistrationNumber }).IsUnique();
            entity.Property(x => x.RegistrationNumber).HasMaxLength(32);
            entity.Property(x => x.DisplayName).HasMaxLength(128);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<Driver>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.LicenseNumber }).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.Property(x => x.LicenseNumber).HasMaxLength(64);
            entity.Property(x => x.PhoneNumber).HasMaxLength(40);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<GpsDevice>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.SerialNumber }).IsUnique();
            entity.Property(x => x.SerialNumber).HasMaxLength(64);
            entity.Property(x => x.DisplayName).HasMaxLength(128);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        builder.Entity<DeviceAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.DeviceId, x.UnassignedAtUtc })
                .IsUnique()
                .HasFilter("[UnassignedAtUtc] IS NULL");
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.UnassignedAtUtc });
            entity.Property(x => x.AssignedAtUtc).HasPrecision(7);
            entity.Property(x => x.UnassignedAtUtc).HasPrecision(7);
        });

        builder.Entity<TelemetryPoint>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.EventId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.RecordedAtUtc });
            entity.Property(x => x.DeviceId).HasMaxLength(64);
            entity.Property(x => x.EventId).HasMaxLength(128);
            entity.Property(x => x.RecordedAtUtc).HasPrecision(7);
            entity.Property(x => x.IngestedAtUtc).HasPrecision(7);
            entity.Property(x => x.HeadingDegrees).HasPrecision(6, 2);
        });

        builder.Entity<CurrentVehiclePosition>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId }).IsUnique();
            entity.Property(x => x.DeviceId).HasMaxLength(64);
            entity.Property(x => x.EventId).HasMaxLength(128);
            entity.Property(x => x.RecordedAtUtc).HasPrecision(7);
            entity.Property(x => x.HeadingDegrees).HasPrecision(6, 2);
        });
    }
}
