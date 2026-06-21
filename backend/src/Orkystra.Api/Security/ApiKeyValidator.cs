using Microsoft.Extensions.Options;

namespace Orkystra.Api.Security;

public sealed class ApiKeyValidator
{
    private readonly ApiSecurityOptions _options;

    public ApiKeyValidator(IOptions<ApiSecurityOptions> options)
    {
        _options = options.Value;
    }

    public string HeaderName => _options.ApiKeyHeaderName;

    public bool IsValid(string? providedApiKey) =>
        !string.IsNullOrWhiteSpace(_options.ApiKey) &&
        string.Equals(providedApiKey, _options.ApiKey, StringComparison.Ordinal);
}
