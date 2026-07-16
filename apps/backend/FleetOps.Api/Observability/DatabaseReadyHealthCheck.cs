using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FleetOps.Api.Observability;

public sealed class DatabaseReadyHealthCheck(FleetOpsDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = dbContext.Database.IsInMemory()
                || await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database connection is ready.")
                : HealthCheckResult.Unhealthy("Database connection is unavailable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database readiness check failed.", ex);
        }
    }
}
