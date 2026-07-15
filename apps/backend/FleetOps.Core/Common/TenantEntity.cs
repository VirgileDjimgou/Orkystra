namespace FleetOps.Core.Common;

public abstract class TenantEntity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public Guid OrganizationId { get; protected init; }
    public DateTimeOffset CreatedAtUtc { get; protected init; } = DateTimeOffset.UtcNow;
}
