using System.Security.Claims;
using System.Text.Encodings.Web;
using FleetOps.Infrastructure.Integrations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Security;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyCredentialService apiKeyCredentialService) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var presentedKey = headerValues.ToString();
        var validation = await apiKeyCredentialService.ValidateAsync(presentedKey, Context.RequestAborted);
        if (validation is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new List<Claim>
        {
            new(TenantClaimTypes.OrganizationId, validation.OrganizationId.ToString()),
            new("api_key_credential_id", validation.CredentialId.ToString()),
            new("api_key_name", validation.CredentialName),
            new("api_key_type", validation.CredentialType.ToString()),
        };
        claims.AddRange(validation.Scopes.Select(scope => new Claim("scope", scope)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
