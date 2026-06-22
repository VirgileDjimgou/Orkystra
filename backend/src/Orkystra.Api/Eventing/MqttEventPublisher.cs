using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using Orkystra.Contracts.Eventing;

namespace Orkystra.Api.Eventing;

public sealed class MqttEventPublisher : IEventBackbonePublisher
{
    private readonly EventBackboneOptions _options;
    private readonly MqttEnvelopeSerializer _serializer;
    private readonly EventBackboneTelemetryStore _telemetryStore;
    private readonly ILogger<MqttEventPublisher> _logger;

    public MqttEventPublisher(
        IOptions<EventBackboneOptions> options,
        MqttEnvelopeSerializer serializer,
        EventBackboneTelemetryStore telemetryStore,
        ILogger<MqttEventPublisher> logger)
    {
        _options = options.Value;
        _serializer = serializer;
        _telemetryStore = telemetryStore;
        _logger = logger;
    }

    public async ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!_options.Enabled)
        {
            throw new InvalidOperationException("The MQTT event backbone is disabled in the current runtime configuration.");
        }

        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var clientOptions = MqttConnectionSettings.BuildClientOptions(_options.BrokerUrl, "orkystra-publisher");
        var serializedEnvelope = _serializer.Serialize(envelope);

        await client.ConnectAsync(clientOptions, cancellationToken);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(envelope.Topic)
            .WithPayload(serializedEnvelope)
            .WithContentType("application/json")
            .Build();

        await client.PublishAsync(message, cancellationToken);
        await client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);

        _telemetryStore.RecordPublished(envelope.Topic, envelope.EventType, DateTimeOffset.UtcNow);

        _logger.LogInformation("Published event {EventType} to MQTT topic {Topic}.", envelope.EventType, envelope.Topic);
    }
}
