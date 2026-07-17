namespace FleetOps.Core.Modules.Operations;

public interface IPrivateMediaStorage
{
    Task<long> GetLengthAsync(string storageKey, CancellationToken cancellationToken);
    Task<string> GetSha256Async(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
    Task AppendAsync(string storageKey, long expectedOffset, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
    Task MoveAsync(string sourceStorageKey, string destinationStorageKey, CancellationToken cancellationToken);
    Task<(Stream Stream, string ContentType, string FileName)> OpenReadAsync(
        string storageKey,
        string contentType,
        string fileName,
        CancellationToken cancellationToken);
}
