using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Operations;

public sealed class DeliveryProofPhoto : TenantEntity
{
    private DeliveryProofPhoto() { }

    public DeliveryProofPhoto(
        Guid organizationId,
        Guid deliveryProofId,
        Guid mediaAssetId,
        string? caption)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        if (deliveryProofId == Guid.Empty)
        {
            throw new ArgumentException("Delivery proof is required.", nameof(deliveryProofId));
        }

        if (mediaAssetId == Guid.Empty)
        {
            throw new ArgumentException("Media asset is required.", nameof(mediaAssetId));
        }

        OrganizationId = organizationId;
        DeliveryProofId = deliveryProofId;
        MediaAssetId = mediaAssetId;
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
    }

    public Guid DeliveryProofId { get; private init; }
    public Guid MediaAssetId { get; private init; }
    public string? Caption { get; private init; }
}
