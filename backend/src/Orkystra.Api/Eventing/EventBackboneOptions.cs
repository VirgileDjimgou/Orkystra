namespace Orkystra.Api.Eventing;

public sealed class EventBackboneOptions
{
    public const string SectionName = "EventBackbone";

    public bool Enabled { get; set; } = true;

    public string BrokerUrl { get; set; } = "mqtt://localhost:1883";

    public string SimulationTopicFilter { get; set; } = "orkystra/events/simulation/#";

    public int ReconnectDelaySeconds { get; set; } = 5;
}
