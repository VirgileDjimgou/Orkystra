using FleetOps.Core.Modules.Dispatch;

namespace FleetOps.Infrastructure.RecipientStatus;

public interface IRecipientStatusNotificationService
{
    Task QueueMissionStatusAsync(Guid organizationId, Guid missionId, MissionStatus status, CancellationToken cancellationToken);
    Task<RecipientStatusNotificationDispatchResult> DispatchPendingAsync(CancellationToken cancellationToken);
}

public sealed record RecipientStatusNotificationDispatchResult(int Delivered, int Retried, int DeadLettered);
