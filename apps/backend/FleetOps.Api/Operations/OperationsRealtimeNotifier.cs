using Microsoft.AspNetCore.SignalR;

namespace FleetOps.Api.Operations;

public interface IOperationsRealtimeNotifier
{
    Task NotifyQueueChangedAsync(Guid organizationId, string reason, CancellationToken cancellationToken);
}

public sealed class OperationsRealtimeNotifier(IHubContext<OperationsHub> hubContext) : IOperationsRealtimeNotifier
{
    public Task NotifyQueueChangedAsync(Guid organizationId, string reason, CancellationToken cancellationToken) =>
        hubContext.Clients.Group($"organization:{organizationId}:operations")
            .SendAsync("operationsQueueChanged", reason, cancellationToken);
}
