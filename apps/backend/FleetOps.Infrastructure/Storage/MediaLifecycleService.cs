using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Infrastructure.Storage;

public sealed class MediaLifecycleService(FleetOpsDbContext dbContext, IPrivateMediaStorage storage)
{
    public async Task<MediaLifecycleResult> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var expiredAssets = await dbContext.MediaAssets
            .Where(x => !x.IsReadRevoked && x.RetainUntilUtc <= nowUtc)
            .ToListAsync(cancellationToken);
        var expiredSessions = await dbContext.MediaUploadSessions
            .Where(x => !x.IsCompleted && x.ExpiresAtUtc <= nowUtc)
            .ToListAsync(cancellationToken);

        foreach (var asset in expiredAssets)
        {
            await storage.DeleteAsync(asset.StorageKey, cancellationToken);
            asset.RevokeReadAccess(nowUtc);
        }
        foreach (var session in expiredSessions)
        {
            await storage.DeleteAsync(session.TempStorageKey, cancellationToken);
            await storage.DeleteAsync($"tenants/{session.OrganizationId:N}/media/{session.Id:N}", cancellationToken);
            if (session.ScanDisposition == UploadedContentDisposition.Quarantine)
                await storage.DeleteAsync($"tenants/{session.OrganizationId:N}/quarantine/{session.Id:N}", cancellationToken);
        }
        dbContext.MediaUploadSessions.RemoveRange(expiredSessions);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MediaLifecycleResult(expiredAssets.Count, expiredSessions.Count);
    }
}

public sealed record MediaLifecycleResult(int DeletedAssets, int DeletedUploadSessions);
