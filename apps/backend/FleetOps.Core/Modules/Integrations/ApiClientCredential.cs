using FleetOps.Core.Common;

namespace FleetOps.Core.Modules.Integrations;

public sealed class ApiClientCredential : TenantEntity
{
    private ApiClientCredential() { }

    public ApiClientCredential(
        Guid organizationId,
        string name,
        ApiClientCredentialType credentialType,
        IReadOnlyCollection<string> scopes,
        string keyId,
        string secretHash,
        string secretPreview)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        OrganizationId = organizationId;
        Name = RequireText(name, nameof(name), 120);
        CredentialType = credentialType;
        ScopeList = NormalizeScopes(scopes);
        KeyId = RequireText(keyId, nameof(keyId), 48);
        SecretHash = RequireText(secretHash, nameof(secretHash), 128);
        SecretPreview = RequireText(secretPreview, nameof(secretPreview), 12);
    }

    public string Name { get; private set; } = string.Empty;
    public ApiClientCredentialType CredentialType { get; private set; }
    public string ScopeList { get; private set; } = string.Empty;
    public string KeyId { get; private set; } = string.Empty;
    public string SecretHash { get; private set; } = string.Empty;
    public string SecretPreview { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastUsedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public long RowVersion { get; private set; }

    public IReadOnlyList<string> GetScopes() =>
        ScopeList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void MarkUsed(DateTimeOffset usedAtUtc)
    {
        LastUsedAtUtc = usedAtUtc.ToUniversalTime();
        RowVersion++;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Credential is already revoked.");
        }

        IsActive = false;
        RevokedAtUtc = revokedAtUtc.ToUniversalTime();
        RowVersion++;
    }

    private static string NormalizeScopes(IReadOnlyCollection<string> scopes)
    {
        if (scopes.Count == 0)
        {
            throw new ArgumentException("At least one scope is required.", nameof(scopes));
        }

        var normalized = scopes
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one valid scope is required.", nameof(scopes));
        }

        return string.Join(',', normalized);
    }

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return trimmed;
    }
}
