using FleetOps.Api.Media;
using FleetOps.Infrastructure.Storage;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class MediaUrlSignerTests
{
    private readonly MediaUrlSigner _signer = new(Options.Create(new ObjectStorageOptions
    {
        MediaSigningKey = "media-url-test-key-with-at-least-32-characters"
    }));

    [Fact]
    public void CapabilityIsTenantBoundAndShortLived()
    {
        var assetId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var url = new Uri("https://fleetops.invalid" + _signer.CreateReadUrl(assetId, organizationId, TimeSpan.FromMinutes(10)));
        var query = QueryHelpers.ParseQuery(url.Query);
        var expires = long.Parse(query["expires"]!, System.Globalization.CultureInfo.InvariantCulture);
        var signature = query["signature"].ToString();

        Assert.True(_signer.IsValid(assetId, organizationId, expires, signature));
        Assert.False(_signer.IsValid(assetId, Guid.NewGuid(), expires, signature));
        Assert.False(_signer.IsValid(assetId, organizationId, DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds(), signature));
        Assert.Throws<ArgumentOutOfRangeException>(() => _signer.CreateReadUrl(assetId, organizationId, TimeSpan.FromMinutes(16)));
    }
}
