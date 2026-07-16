using FleetOps.Core.Modules.Integrations;

namespace FleetOps.Infrastructure.Integrations;

public interface IApiKeyCredentialService
{
    Task<ApiClientCredentialIssueResult> CreateAsync(
        Guid organizationId,
        string name,
        ApiClientCredentialType credentialType,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiClientCredential>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<ApiClientCredential?> RevokeAsync(Guid organizationId, Guid credentialId, CancellationToken cancellationToken);
    Task<ApiKeyValidationResult?> ValidateAsync(string presentedKey, CancellationToken cancellationToken);
}
