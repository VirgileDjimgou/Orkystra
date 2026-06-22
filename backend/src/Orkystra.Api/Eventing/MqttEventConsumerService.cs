using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using Orkystra.Api.Connectors;

namespace Orkystra.Api.Eventing;

public sealed class MqttEventConsumerService : BackgroundService
{
    private readonly EventBackboneOptions _options;
    private readonly ProviderRuntimeStore _runtimeStore;
    private readonly EventBackboneMessageDispatcher _dispatcher;
    private readonly EventBackboneTelemetryStore _telemetryStore;
    private readonly ILogger<MqttEventConsumerService> _logger;

    public MqttEventConsumerService(
        IOptions<EventBackboneOptions> options,
        ProviderRuntimeStore runtimeStore,
        EventBackboneMessageDispatcher dispatcher,
        EventBackboneTelemetryStore telemetryStore,
        ILogger<MqttEventConsumerService> logger)
    {
        _options = options.Value;
        _runtimeStore = runtimeStore;
        _dispatcher = dispatcher;
        _telemetryStore = telemetryStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("MQTT event backbone consumer is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new MqttFactory();
                using var client = factory.CreateMqttClient();

                client.ApplicationMessageReceivedAsync += async eventArgs =>
                {
                    await _dispatcher.DispatchAsync(
                        eventArgs.ApplicationMessage.Topic,
                        eventArgs.ApplicationMessage.PayloadSegment,
                        stoppingToken);
                };

                var clientOptions = MqttConnectionSettings.BuildClientOptions(_options.BrokerUrl, "orkystra-consumer");
                await client.ConnectAsync(clientOptions, stoppingToken);
                foreach (var topicFilter in BuildTopicFilters())
                {
                    var subscriptionOptions = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(topicFilter)
                        .Build();
                    await client.SubscribeAsync(subscriptionOptions, stoppingToken);
                }

                _logger.LogInformation(
                    "Connected to MQTT broker {BrokerUrl} and subscribed to {TopicCount} active topic filters.",
                    _options.BrokerUrl,
                    BuildTopicFilters().Count);

                while (!stoppingToken.IsCancellationRequested && client.IsConnected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _telemetryStore.RecordError(exception.Message);
                _logger.LogWarning(
                    exception,
                    "MQTT consumer loop failed. Retrying in {DelaySeconds} seconds.",
                    _options.ReconnectDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
            }
        }
    }

    private IReadOnlyCollection<string> BuildTopicFilters()
    {
        var topicFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            _options.SimulationTopicFilter
        };

        var gpsRuntime = _runtimeStore.GetProvider("gps-telematics-adapter");
        if (gpsRuntime is not null &&
            gpsRuntime.Settings.TryGetValue("streamTopic", out var streamTopic) &&
            !string.IsNullOrWhiteSpace(streamTopic))
        {
            topicFilters.Add(streamTopic.Trim());
        }

        return topicFilters.ToArray();
    }
}
