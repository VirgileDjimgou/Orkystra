namespace FleetOps.Infrastructure.Integrations;

public interface IWebhookDispatchService
{
    Task<WebhookDispatchResult> DispatchPendingAsync(CancellationToken cancellationToken);
}
