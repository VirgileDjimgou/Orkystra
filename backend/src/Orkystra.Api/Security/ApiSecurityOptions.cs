namespace Orkystra.Api.Security;

public sealed class ApiSecurityOptions
{
    public const string SectionName = "Security";

    public string ApiKeyHeaderName { get; init; } = "X-Api-Key";

    public string ApiKey { get; init; } = string.Empty;
}
