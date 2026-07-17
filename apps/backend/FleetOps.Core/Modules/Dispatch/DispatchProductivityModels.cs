using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Dispatch;

public sealed class MissionTemplate : TenantEntity
{
    private readonly List<MissionTemplateStop> _stops = [];
    private MissionTemplate() { }
    public MissionTemplate(Guid organizationId, string name, string title, IEnumerable<(int Sequence, string Name, string Address, int ArrivalOffsetMinutes)> stops)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("A template name of up to 100 characters is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160) throw new ArgumentException("A mission title is required.", nameof(title));
        var ordered = stops.OrderBy(x => x.Sequence).ToList();
        if (ordered.Count == 0 || ordered.Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, ordered.Count)) is false) throw new ArgumentException("Template stops must have a contiguous sequence.", nameof(stops));
        OrganizationId = organizationId; Name = name.Trim(); Title = title.Trim(); _stops.AddRange(ordered.Select(x => new MissionTemplateStop(organizationId, Id, x.Sequence, x.Name, x.Address, x.ArrivalOffsetMinutes)));
    }
    public string Name { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public long RowVersion { get; private set; }
    public IReadOnlyCollection<MissionTemplateStop> Stops => _stops;
}

public sealed class MissionTemplateStop : TenantEntity
{
    private MissionTemplateStop() { }
    public MissionTemplateStop(Guid organizationId, Guid templateId, int sequence, string name, string address, int arrivalOffsetMinutes)
    {
        if (organizationId == Guid.Empty || templateId == Guid.Empty || sequence < 1 || arrivalOffsetMinutes < 0) throw new ArgumentException("Valid template stop values are required.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Stop name and address are required.");
        OrganizationId = organizationId; TemplateId = templateId; Sequence = sequence; Name = name.Trim(); Address = address.Trim(); ArrivalOffsetMinutes = arrivalOffsetMinutes;
    }
    public Guid TemplateId { get; private init; }
    public int Sequence { get; private init; }
    public string Name { get; private init; } = string.Empty;
    public string Address { get; private init; } = string.Empty;
    public int ArrivalOffsetMinutes { get; private init; }
}

public sealed class DispatchImportReceipt : TenantEntity
{
    private DispatchImportReceipt() { }
    public DispatchImportReceipt(Guid organizationId, string importKey, DateTimeOffset importedAtUtc)
    {
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(importKey) || importKey.Trim().Length > 120) throw new ArgumentException("A valid organization and import key are required.");
        OrganizationId = organizationId; ImportKey = importKey.Trim(); ImportedAtUtc = importedAtUtc.ToUniversalTime();
    }
    public string ImportKey { get; private init; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; private init; }
}

public sealed class DispatchSavedView : TenantEntity
{
    private DispatchSavedView() { }
    public DispatchSavedView(Guid organizationId, Guid userId, string name, string filterJson)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("A valid saved view is required.");
        OrganizationId = organizationId; UserId = userId; Name = name.Trim(); FilterJson = filterJson ?? "{}";
    }
    public Guid UserId { get; private init; }
    public string Name { get; private init; } = string.Empty;
    public string FilterJson { get; private init; } = "{}";
}
