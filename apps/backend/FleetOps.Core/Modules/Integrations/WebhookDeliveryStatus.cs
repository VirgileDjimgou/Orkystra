namespace FleetOps.Core.Modules.Integrations;

public enum WebhookDeliveryStatus
{
    Pending = 1,
    Delivered = 2,
    DeadLetter = 3,
}
