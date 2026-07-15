using System.Text.Json;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Persistence;

namespace FleetOps.Api.Auditing;

public interface IAuditService
{
    Task WriteAsync(
        Guid organizationId,
        Guid? actorUserId,
        string actionType,
        string targetType,
        string? targetId,
        object? metadata,
        CancellationToken cancellationToken);
}

public sealed class AuditService(FleetOpsDbContext dbContext) : IAuditService
{
    public async Task WriteAsync(
        Guid organizationId,
        Guid? actorUserId,
        string actionType,
        string targetType,
        string? targetId,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var payload = metadata is null ? null : JsonSerializer.Serialize(metadata);
        dbContext.AuditLogs.Add(new AuditLog(organizationId, actorUserId, actionType, targetType, targetId, payload));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
