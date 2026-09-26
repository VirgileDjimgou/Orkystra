using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure.Tracking;

public sealed class TrackingRetentionOptions
{
    public const string SectionName = "Tracking";

    public int RetentionDays { get; init; } = 7;
    public int RetentionBatchSize { get; init; } = 1000;
}

public interface ITelemetryRetentionService
{
    Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed class TelemetryRetentionService(
    FleetOpsDbContext dbContext,
    IOptions<TrackingRetentionOptions> options) : ITelemetryRetentionService
{
    public async Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoffUtc = now.AddDays(-Math.Max(1, options.Value.RetentionDays));
        var batchSize = Math.Clamp(options.Value.RetentionBatchSize, 1, 10_000);
        var expired = dbContext.TelemetryPoints
            .Where(x => x.RecordedAtUtc < cutoffUtc)
            .OrderBy(x => x.RecordedAtUtc)
            .Take(batchSize);

        if (dbContext.Database.IsRelational())
        {
            return await expired.ExecuteDeleteAsync(cancellationToken);
        }

        var batch = await expired.ToListAsync(cancellationToken);
        if (batch.Count == 0) return 0;
        dbContext.TelemetryPoints.RemoveRange(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
        return batch.Count;
    }
}
