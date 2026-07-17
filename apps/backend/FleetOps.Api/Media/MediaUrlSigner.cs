using System.Security.Cryptography;
using System.Text;
using FleetOps.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Media;

public interface IMediaUrlSigner
{
    string CreateReadUrl(Guid assetId, Guid organizationId, TimeSpan lifetime);
    bool IsValid(Guid assetId, Guid organizationId, long expiresUnixSeconds, string signature);
}

public sealed class MediaUrlSigner(IOptions<ObjectStorageOptions> options) : IMediaUrlSigner
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.MediaSigningKey);

    public string CreateReadUrl(Guid assetId, Guid organizationId, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(15))
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Media read URLs must live for at most 15 minutes.");
        var expires = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var signature = Sign(assetId, organizationId, expires);
        return $"/api/v1/media/{assetId}?expires={expires}&signature={Uri.EscapeDataString(signature)}";
    }

    public bool IsValid(Guid assetId, Guid organizationId, long expiresUnixSeconds, string signature)
    {
        if (expiresUnixSeconds < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return false;
        }

        var expected = Sign(assetId, organizationId, expiresUnixSeconds);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private string Sign(Guid assetId, Guid organizationId, long expiresUnixSeconds)
    {
        using var hmac = new HMACSHA256(_key);
        var payload = Encoding.UTF8.GetBytes($"{assetId:D}:{organizationId:D}:{expiresUnixSeconds}");
        return Convert.ToBase64String(hmac.ComputeHash(payload));
    }
}
