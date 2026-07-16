using FleetOps.Core.Modules.Integrations;

namespace FleetOps.Infrastructure.Integrations;

public sealed record ApiKeyValidationResult(
    Guid OrganizationId,
    Guid CredentialId,
    string CredentialName,
    ApiClientCredentialType CredentialType,
    IReadOnlyList<string> Scopes);
