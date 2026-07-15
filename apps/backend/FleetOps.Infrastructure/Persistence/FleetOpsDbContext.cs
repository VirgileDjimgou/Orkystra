using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Tracking;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Infrastructure.Persistence;

public sealed class FleetOpsDbContext(DbContextOptions<FleetOpsDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<TelemetryPoint> TelemetryPoints => Set<TelemetryPoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.RegistrationNumber }).IsUnique();
            entity.Property(x => x.RegistrationNumber).HasMaxLength(32);
            entity.Property(x => x.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<TelemetryPoint>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.RecordedAtUtc });
            entity.Property(x => x.DeviceId).HasMaxLength(128);
        });
    }
}
