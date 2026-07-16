using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class ChecklistTemplate : TenantEntity
{
    private readonly List<ChecklistTemplateItem> _items = [];

    private ChecklistTemplate() { }

    public ChecklistTemplate(Guid organizationId, string code, string name)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        OrganizationId = organizationId;
        Code = RequireNonEmpty(code, nameof(code));
        Name = RequireNonEmpty(name, nameof(name));
        IsActive = true;
    }

    public string Code { get; private init; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<ChecklistTemplateItem> Items => _items;

    public void AddItem(ChecklistTemplateItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.OrganizationId != OrganizationId)
        {
            throw new InvalidOperationException("Checklist items must belong to the same organization.");
        }

        if (_items.Any(existing => existing.Sequence == item.Sequence))
        {
            throw new InvalidOperationException("Checklist item sequence must be unique.");
        }

        if (_items.Any(existing => existing.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Checklist item code must be unique inside the template.");
        }

        _items.Add(item);
    }

    public void Deactivate() => IsActive = false;

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
