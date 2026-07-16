using System.Text.Json;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Infrastructure.Integrations;

public sealed class IntegrationOutboxService(
    FleetOpsDbContext dbContext,
    TimeProvider timeProvider) : IIntegrationOutboxService
{
    public async Task<int> PublishAsync(
        Guid organizationId,
        string eventType,
        string aggregateType,
        string aggregateId,
        object payload,
        CancellationToken cancellationToken)
    {
        var endpoints = await dbContext.WebhookEndpoints
            .Where(x => x.OrganizationId == organizationId && x.IsActive && x.EventType == eventType)
            .ToListAsync(cancellationToken);
        if (endpoints.Count == 0)
        {
            return 0;
        }

        var payloadJson = JsonSerializer.Serialize(payload);
        var occurredAtUtc = timeProvider.GetUtcNow();
        foreach (var endpoint in endpoints)
        {
            dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage(
                organizationId,
                endpoint.Id,
                eventType,
                aggregateType,
                aggregateId,
                payloadJson,
                occurredAtUtc));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return endpoints.Count;
    }
}
