using FleetOps.Core.Modules.Operations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure.Storage;

public sealed class FileSystemPrivateMediaStorage(
    IOptions<ObjectStorageOptions> options,
    IHostEnvironment hostEnvironment) : IPrivateMediaStorage
{
    private readonly string _rootPath = ResolveRootPath(options.Value.RootPath, hostEnvironment.ContentRootPath);

    public Task<long> GetLengthAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        return Task.FromResult(File.Exists(path) ? new FileInfo(path).Length : 0L);
    }

    public async Task AppendAsync(
        string storageKey,
        long expectedOffset,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (expectedOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedOffset), "Expected offset cannot be negative.");
        }

        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var actualLength = File.Exists(path) ? new FileInfo(path).Length : 0L;
        if (actualLength != expectedOffset)
        {
            throw new InvalidOperationException($"Upload offset mismatch. Expected {expectedOffset} bytes, found {actualLength}.");
        }

        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await stream.WriteAsync(content, cancellationToken);
    }

    public Task MoveAsync(string sourceStorageKey, string destinationStorageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = ResolvePath(sourceStorageKey);
        var destination = ResolvePath(destinationStorageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Move(source, destination);
        return Task.CompletedTask;
    }

    public Task<(Stream Stream, string ContentType, string FileName)> OpenReadAsync(
        string storageKey,
        string contentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Media file was not found.", path);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult((stream, contentType, fileName));
    }

    private string ResolvePath(string storageKey)
    {
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        if (!path.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved storage path escapes the configured storage root.");
        }

        return path;
    }

    private static string ResolveRootPath(string configuredRoot, string contentRoot)
    {
        var root = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(contentRoot, configuredRoot);
        return Path.GetFullPath(root);
    }
}
