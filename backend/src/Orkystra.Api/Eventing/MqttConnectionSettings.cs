using MQTTnet.Client;

namespace Orkystra.Api.Eventing;

internal static class MqttConnectionSettings
{
    public static MqttClientOptions BuildClientOptions(string brokerUrl, string clientIdPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerUrl);
        var brokerUri = new Uri(brokerUrl, UriKind.Absolute);
        var port = brokerUri.IsDefaultPort ? 1883 : brokerUri.Port;

        return new MqttClientOptionsBuilder()
            .WithClientId($"{clientIdPrefix}-{Guid.NewGuid():N}")
            .WithTcpServer(brokerUri.Host, port)
            .Build();
    }
}
