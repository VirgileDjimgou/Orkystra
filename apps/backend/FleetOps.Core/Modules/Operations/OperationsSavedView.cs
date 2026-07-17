using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class OperationsSavedView : TenantEntity
{
    private OperationsSavedView() { }

    public OperationsSavedView(
        Guid organizationId,
        Guid createdByUserId,
        string name,
        string filterJson,
        bool isShared)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Creator is required.", nameof(createdByUserId));
        }

        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;
        Name = RequireNonEmpty(name, nameof(name), 120);
        FilterJson = RequireNonEmpty(filterJson, nameof(filterJson), 2000);
        IsShared = isShared;
    }

    public Guid CreatedByUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string FilterJson { get; private set; } = string.Empty;
    public bool IsShared { get; private set; }
    public long RowVersion { get; private set; }

    public void Update(string name, string filterJson, bool isShared)
    {
        Name = RequireNonEmpty(name, nameof(name), 120);
        FilterJson = RequireNonEmpty(filterJson, nameof(filterJson), 2000);
        IsShared = isShared;
        RowVersion++;
    }

    private static string RequireNonEmpty(string value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
