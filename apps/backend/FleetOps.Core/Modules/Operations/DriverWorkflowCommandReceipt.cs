using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class DriverWorkflowCommandReceipt : TenantEntity
{
    private DriverWorkflowCommandReceipt() { }

    public DriverWorkflowCommandReceipt(
        Guid organizationId,
        Guid driverId,
        string commandId,
        string scopeType,
        string scopeId,
        DateTimeOffset processedAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver is required.", nameof(driverId));
        }

        OrganizationId = organizationId;
        DriverId = driverId;
        CommandId = RequireNonEmpty(commandId, nameof(commandId));
        ScopeType = RequireNonEmpty(scopeType, nameof(scopeType));
        ScopeId = RequireNonEmpty(scopeId, nameof(scopeId));
        ProcessedAtUtc = processedAtUtc.ToUniversalTime();
    }

    public Guid DriverId { get; private init; }
    public string CommandId { get; private init; } = string.Empty;
    public string ScopeType { get; private init; } = string.Empty;
    public string ScopeId { get; private init; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; private init; }

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
