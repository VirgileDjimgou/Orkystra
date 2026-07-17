using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Operations;

public interface IDriverSyncIncidentService
{
    Task RecordAsync(
        Guid organizationId,
        Guid missionId,
        Guid driverId,
        string scopeType,
        string incidentCode,
        string severity,
        string message,
        string? commandId,
        CancellationToken cancellationToken);
}

public sealed class DriverSyncIncidentService(
    FleetOpsDbContext dbContext,
    IOperationsRealtimeNotifier notifier) : IDriverSyncIncidentService
{
    public async Task RecordAsync(
        Guid organizationId,
        Guid missionId,
        Guid driverId,
        string scopeType,
        string incidentCode,
        string severity,
        string message,
        string? commandId,
        CancellationToken cancellationToken)
    {
        var incidentKey = $"{scopeType}:{missionId:D}:{incidentCode}".ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.DriverSyncExceptionIncidents
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.IncidentKey == incidentKey,
                cancellationToken);

        if (existing is null)
        {
            dbContext.DriverSyncExceptionIncidents.Add(new DriverSyncExceptionIncident(
                organizationId,
                missionId,
                driverId,
                incidentKey,
                incidentCode,
                severity,
                scopeType,
                message,
                commandId,
                now));
        }
        else
        {
            existing.RecordOccurrence(message, severity, commandId, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await notifier.NotifyQueueChangedAsync(organizationId, "driver-sync-incident", cancellationToken);
    }
}
