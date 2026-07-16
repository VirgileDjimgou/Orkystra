namespace FleetOps.Infrastructure.Integrations;

public sealed record WebhookDispatchResult(
    int Delivered,
    int Retried,
    int DeadLettered);
