using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Persistence;
using FleetOps.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace FleetOps.UnitTests;

public sealed class MediaStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fleetops-media-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task FileSystemContractMakesChunkRetryIdempotentAndChecksumsContent()
    {
        var storage = Storage(Path.Combine(_root, "contract"));
        var key = $"tenants/{Guid.NewGuid():N}/temp/{Guid.NewGuid():N}";
        var first = "first"u8.ToArray();
        var second = "second"u8.ToArray();
        await storage.AppendAsync(key, 0, first, CancellationToken.None);
        await storage.AppendAsync(key, 0, first, CancellationToken.None);
        await storage.AppendAsync(key, first.Length, second, CancellationToken.None);

        Assert.Equal(first.Length + second.Length, await storage.GetLengthAsync(key, CancellationToken.None));
        Assert.Equal("da83f63e1a473003712c18f5afc5a79044221943d1083c7c5a7ac7236d85e8d2", await storage.GetSha256Async(key, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.AppendAsync(key, 0, "other"u8.ToArray(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.GetLengthAsync("../escape", CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Minio")]
    public async Task S3ContractSupportsResumeChecksumMoveReadAndDelete()
    {
        var endpoint = Environment.GetEnvironmentVariable("FLEETOPS_TEST_MINIO_ENDPOINT") ?? "http://localhost:9010";
        var accessKey = Environment.GetEnvironmentVariable("FLEETOPS_TEST_MINIO_ACCESS_KEY") ?? "fleetops-media";
        var secretKey = Environment.GetEnvironmentVariable("FLEETOPS_TEST_MINIO_SECRET_KEY") ?? "ChangeThisMedia_LocalOnly";
        var storage = new S3PrivateMediaStorage(Options.Create(new ObjectStorageOptions
        {
            Provider = "S3",
            ServiceUrl = endpoint,
            BucketName = "fleetops-private-media",
            AccessKey = accessKey,
            SecretKey = secretKey
        }));
        var organizationId = Guid.NewGuid();
        var temporaryKey = $"tenants/{organizationId:N}/temp/{Guid.NewGuid():N}";
        var mediaKey = $"tenants/{organizationId:N}/media/{Guid.NewGuid():N}";
        var first = "object-"u8.ToArray();
        var second = "storage"u8.ToArray();
        try
        {
            await storage.AppendAsync(temporaryKey, 0, first, CancellationToken.None);
            await storage.AppendAsync(temporaryKey, 0, first, CancellationToken.None);
            await storage.AppendAsync(temporaryKey, first.Length, second, CancellationToken.None);
            var checksum = await storage.GetSha256Async(temporaryKey, CancellationToken.None);
            await storage.MoveAsync(temporaryKey, mediaKey, CancellationToken.None);
            var (stream, _, _) = await storage.OpenReadAsync(mediaKey, "application/octet-stream", "proof", CancellationToken.None);
            await using (stream)
            using (var reader = new StreamReader(stream))
                Assert.Equal("object-storage", await reader.ReadToEndAsync(CancellationToken.None));
            Assert.Equal(64, checksum.Length);
        }
        finally
        {
            await storage.DeleteAsync(temporaryKey, CancellationToken.None);
            await storage.DeleteAsync(mediaKey, CancellationToken.None);
        }
    }

    [Fact]
    public async Task S3OutageFailsWithoutFallingBackToLocalDisk()
    {
        var storage = new S3PrivateMediaStorage(Options.Create(new ObjectStorageOptions
        {
            Provider = "S3",
            ServiceUrl = "http://127.0.0.1:9",
            BucketName = "fleetops-private-media",
            AccessKey = "unavailable",
            SecretKey = "unavailable-secret-key"
        }));
        await Assert.ThrowsAsync<HttpRequestException>(() => storage.GetLengthAsync(
            $"tenants/{Guid.NewGuid():N}/media/{Guid.NewGuid():N}", CancellationToken.None));
    }

    [Fact]
    public async Task LegacyMigrationIsReplayableVerifiedAndPreservesSource()
    {
        var source = Storage(Path.Combine(_root, "source"));
        var destination = Storage(Path.Combine(_root, "destination"));
        var options = new ObjectStorageOptions { Provider = "S3", RetentionDays = 30 };
        await using var db = Database();
        var organizationId = Guid.NewGuid();
        var legacyKey = $"{organizationId:D}/media/legacy.jpg";
        var bytes = "legacy-media"u8.ToArray();
        await source.AppendAsync(legacyKey, 0, bytes, CancellationToken.None);
        var asset = new MediaAsset(organizationId, legacyKey, "legacy.jpg", "image/jpeg", bytes.Length, "legacy", DateTimeOffset.UtcNow.AddDays(1));
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(CancellationToken.None);
        var service = new MediaMigrationService(db, source, destination, Options.Create(options), TimeProvider.System);

        var first = await service.MigrateAsync(CancellationToken.None);
        var second = await service.MigrateAsync(CancellationToken.None);

        Assert.Equal(1, first.Migrated);
        Assert.Equal(1, second.AlreadyMigrated);
        Assert.Equal(bytes.Length, await source.GetLengthAsync(legacyKey, CancellationToken.None));
        Assert.Equal(64, asset.ChecksumSha256.Length);
        Assert.StartsWith($"tenants/{organizationId:N}/media/", asset.StorageKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetentionRevokesAssetWithoutDeletingReferencedMetadataAndCleansExpiredUpload()
    {
        var storage = Storage(Path.Combine(_root, "lifecycle"));
        await using var db = Database();
        var organizationId = Guid.NewGuid();
        var assetKey = $"tenants/{organizationId:N}/media/{Guid.NewGuid():N}";
        await storage.AppendAsync(assetKey, 0, "media"u8.ToArray(), CancellationToken.None);
        var asset = new MediaAsset(organizationId, assetKey, "proof.jpg", "image/jpeg", 5, new string('a', 64), DateTimeOffset.UtcNow.AddDays(-1));
        var session = new MediaUploadSession(organizationId, Guid.NewGuid(), MediaUploadPurpose.DeliveryProofPhoto, "temp.jpg", "image/jpeg", 4, DateTimeOffset.UtcNow.AddHours(-1), $"tenants/{organizationId:N}/temp/{Guid.NewGuid():N}");
        await storage.AppendAsync(session.TempStorageKey, 0, "temp"u8.ToArray(), CancellationToken.None);
        db.AddRange(asset, session);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await new MediaLifecycleService(db, storage).PurgeExpiredAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(new MediaLifecycleResult(1, 1), result);
        Assert.True(asset.IsReadRevoked);
        Assert.Contains(asset, db.MediaAssets);
        Assert.Empty(db.MediaUploadSessions);
        Assert.Equal(0, await storage.GetLengthAsync(assetKey, CancellationToken.None));
    }

    private FileSystemPrivateMediaStorage Storage(string root) => new(Options.Create(new ObjectStorageOptions { RootPath = root }), new TestEnvironment(_root));
    private static FleetOpsDbContext Database()
    {
        var options = new DbContextOptionsBuilder<FleetOpsDbContext>().UseInMemoryDatabase($"media-{Guid.NewGuid():N}").Options;
        return new FleetOpsDbContext(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "FleetOps.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
