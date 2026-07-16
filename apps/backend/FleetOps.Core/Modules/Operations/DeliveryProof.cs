using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class DeliveryProof : TenantEntity
{
    private readonly List<DeliveryProofPhoto> _photos = [];

    private DeliveryProof() { }

    public DeliveryProof(
        Guid organizationId,
        Guid missionId,
        Guid missionStopId,
        Guid driverId,
        string recipientName,
        string signatureName,
        DateTimeOffset deliveredAtUtc,
        string? notes,
        IEnumerable<DeliveryProofPhoto> photos)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (missionId == Guid.Empty)
        {
            throw new ArgumentException("Mission is required.", nameof(missionId));
        }

        if (missionStopId == Guid.Empty)
        {
            throw new ArgumentException("Mission stop is required.", nameof(missionStopId));
        }

        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver is required.", nameof(driverId));
        }

        OrganizationId = organizationId;
        MissionId = missionId;
        MissionStopId = missionStopId;
        DriverId = driverId;
        RecipientName = RequireNonEmpty(recipientName, nameof(recipientName));
        SignatureName = RequireNonEmpty(signatureName, nameof(signatureName));
        DeliveredAtUtc = deliveredAtUtc.ToUniversalTime();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _photos.AddRange(photos ?? []);
    }

    public Guid MissionId { get; private init; }
    public Guid MissionStopId { get; private init; }
    public Guid DriverId { get; private init; }
    public string RecipientName { get; private init; } = string.Empty;
    public string SignatureName { get; private init; } = string.Empty;
    public DateTimeOffset DeliveredAtUtc { get; private init; }
    public string? Notes { get; private init; }
    public IReadOnlyCollection<DeliveryProofPhoto> Photos => _photos;

    private static string RequireNonEmpty(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameter)
            : value.Trim();
}
