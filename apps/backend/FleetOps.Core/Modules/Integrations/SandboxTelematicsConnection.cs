using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Integrations;

public sealed class SandboxTelematicsConnection : TenantEntity
{
    private SandboxTelematicsConnection() { }

    public SandboxTelematicsConnection(Guid organizationId, string name, DateTimeOffset createdAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        OrganizationId = organizationId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastSucceededAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public string? ResumeCursor { get; private set; }
    public long RowVersion { get; private set; }

    public void RecordSuccess(string cursor, DateTimeOffset occurredAtUtc)
    {
        LastSucceededAtUtc = occurredAtUtc.ToUniversalTime(); LastError = null; ResumeCursor = cursor; RowVersion++;
    }

    public void RecordFailure(string error) { LastError = error.Trim()[..Math.Min(error.Trim().Length, 300)]; RowVersion++; }
    public void SetActive(bool active) { IsActive = active; RowVersion++; }
}
