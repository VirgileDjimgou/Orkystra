namespace FleetOps.Infrastructure.Integrations;

public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";

    public int MaxWebhookAttempts { get; set; } = 3;
    public int RetryBaseDelaySeconds { get; set; } = 5;
}
