using System.Security.Cryptography;
using System.Text;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Infrastructure.Integrations;

public sealed class ApiKeyCredentialService(
    FleetOpsDbContext dbContext,
    TimeProvider timeProvider) : IApiKeyCredentialService
{
    public async Task<ApiClientCredentialIssueResult> CreateAsync(
        Guid organizationId,
        string name,
        ApiClientCredentialType credentialType,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken)
    {
        var keyId = $"fo_{RandomNumberGenerator.GetHexString(12).ToLowerInvariant()}";
        var secret = RandomNumberGenerator.GetHexString(24).ToLowerInvariant();
        var presentedKey = $"{keyId}.{secret}";
        var credential = new ApiClientCredential(
            organizationId,
            name,
            credentialType,
            scopes,
            keyId,
            ComputeHash(presentedKey),
            secret[..8]);

        dbContext.ApiClientCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiClientCredentialIssueResult(credential, presentedKey);
    }

    public async Task<IReadOnlyList<ApiClientCredential>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.ApiClientCredentials
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiClientCredential?> RevokeAsync(Guid organizationId, Guid credentialId, CancellationToken cancellationToken)
    {
        var credential = await dbContext.ApiClientCredentials
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == credentialId, cancellationToken);
        if (credential is null)
        {
            return null;
        }

        credential.Revoke(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return credential;
    }

    public async Task<ApiKeyValidationResult?> ValidateAsync(string presentedKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return null;
        }

        var separator = presentedKey.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return null;
        }

        var keyId = presentedKey[..separator];
        var credential = await dbContext.ApiClientCredentials
            .FirstOrDefaultAsync(x => x.KeyId == keyId && x.IsActive, cancellationToken);
        if (credential is null)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(credential.SecretHash),
                Encoding.UTF8.GetBytes(ComputeHash(presentedKey))))
        {
            return null;
        }

        credential.MarkUsed(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiKeyValidationResult(
            credential.OrganizationId,
            credential.Id,
            credential.Name,
            credential.CredentialType,
            credential.GetScopes());
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
