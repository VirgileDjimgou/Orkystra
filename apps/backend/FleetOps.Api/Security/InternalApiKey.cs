using System.Security.Cryptography;
using System.Text;

namespace FleetOps.Api.Security;

public static class InternalApiKey
{
    public const string HeaderName = "X-FleetOps-Internal-Key";
    public const string ConfigurationKey = "InternalApi:Key";
    public const int MinimumLength = 32;

    public static bool Matches(HttpContext context, IConfiguration configuration)
    {
        var configured = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(configured)) return false;
        var provided = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(provided)) return false;
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return configuredBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}
