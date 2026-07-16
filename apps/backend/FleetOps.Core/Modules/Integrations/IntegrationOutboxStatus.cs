namespace FleetOps.Core.Modules.Integrations;

public enum IntegrationOutboxStatus
{
    Pending = 1,
    Delivered = 2,
    DeadLetter = 3,
}
