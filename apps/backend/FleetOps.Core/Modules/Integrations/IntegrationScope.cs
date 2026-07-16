namespace FleetOps.Core.Modules.Integrations;

public static class IntegrationScope
{
    public const string PartnerFleetRead = "partner.fleet.read";
    public const string PartnerWebhookRead = "partner.webhook.read";
    public const string DeviceTrackingWrite = "device.tracking.write";

    public static readonly string[] All =
    [
        PartnerFleetRead,
        PartnerWebhookRead,
        DeviceTrackingWrite,
    ];
}
