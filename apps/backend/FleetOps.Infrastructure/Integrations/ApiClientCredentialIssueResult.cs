using FleetOps.Core.Modules.Integrations;

namespace FleetOps.Infrastructure.Integrations;

public sealed record ApiClientCredentialIssueResult(
    ApiClientCredential Credential,
    string PlainTextSecret);
