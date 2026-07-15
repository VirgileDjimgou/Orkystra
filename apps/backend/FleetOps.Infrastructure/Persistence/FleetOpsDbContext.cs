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
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<GpsDevice> GpsDevices => Set<GpsDevice>();
    public DbSet<DeviceAssignment> DeviceAssignments => Set<DeviceAssignment>();
    public DbSet<TelemetryPoint> TelemetryPoints => Set<TelemetryPoint>();

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
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.RecordedAtUtc });
            entity.Property(x => x.DeviceId).HasMaxLength(128);
        });
    }
}
