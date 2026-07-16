namespace FleetOps.Infrastructure.Integrations;

public interface IIntegrationOutboxService
{
    Task<int> PublishAsync(
        Guid organizationId,
        string eventType,
        string aggregateType,
        string aggregateId,
        object payload,
        CancellationToken cancellationToken);
}
