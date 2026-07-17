using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure.Storage;

public sealed class MediaMigrationService(
    FleetOpsDbContext dbContext,
    FileSystemPrivateMediaStorage source,
    IPrivateMediaStorage destination,
    IOptions<ObjectStorageOptions> options,
    TimeProvider timeProvider)
{
    public async Task<MediaMigrationReport> MigrateAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(options.Value.Provider, "S3", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Media migration requires ObjectStorage:Provider=S3.");

        var entries = new List<MediaMigrationEntry>();
        var assets = await dbContext.MediaAssets.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        foreach (var asset in assets)
        {
            var destinationKey = $"tenants/{asset.OrganizationId:N}/media/{asset.Id:N}";
            if (string.Equals(asset.StorageKey, destinationKey, StringComparison.Ordinal) && asset.ChecksumSha256.Length == 64)
            {
                entries.Add(new(asset.Id, "already-migrated", null));
                continue;
            }

            try
            {
                var sourceLength = await source.GetLengthAsync(asset.StorageKey, cancellationToken);
                if (sourceLength != asset.SizeBytes) throw new InvalidDataException("Source media length does not match SQL metadata.");
                var sourceChecksum = await source.GetSha256Async(asset.StorageKey, cancellationToken);
                var destinationLength = await destination.GetLengthAsync(destinationKey, cancellationToken);
                if (destinationLength == 0)
                {
                    var (stream, _, _) = await source.OpenReadAsync(asset.StorageKey, asset.ContentType, asset.FileName, cancellationToken);
                    await using (stream)
                    {
                        using var buffer = new MemoryStream();
                        await stream.CopyToAsync(buffer, cancellationToken);
                        await destination.AppendAsync(destinationKey, 0, buffer.ToArray(), cancellationToken);
                    }
                }
                else if (destinationLength != sourceLength)
                {
                    throw new InvalidDataException("Destination media length differs from the source.");
                }

                var destinationChecksum = await destination.GetSha256Async(destinationKey, cancellationToken);
                if (!string.Equals(sourceChecksum, destinationChecksum, StringComparison.Ordinal))
                    throw new InvalidDataException("Destination checksum differs from the source.");

                asset.RecordStorageMigration(destinationKey, destinationChecksum,
                    timeProvider.GetUtcNow().AddDays(Math.Max(1, options.Value.RetentionDays)));
                await dbContext.SaveChangesAsync(cancellationToken);
                entries.Add(new(asset.Id, "migrated", null));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException)
            {
                entries.Add(new(asset.Id, "error", ex.Message));
            }
        }
        return new MediaMigrationReport(entries.Count(x => x.Status == "migrated"), entries.Count(x => x.Status == "already-migrated"), entries.Count(x => x.Status == "error"), entries);
    }
}

public sealed record MediaMigrationEntry(Guid AssetId, string Status, string? Error);
public sealed record MediaMigrationReport(int Migrated, int AlreadyMigrated, int Errors, IReadOnlyList<MediaMigrationEntry> Entries);
