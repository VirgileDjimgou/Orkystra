using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FleetOps.Core.Modules.Operations;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure.Storage;

/// <summary>Minimal AWS Signature V4 S3 client for private MinIO-compatible media objects.</summary>
public sealed class S3PrivateMediaStorage : IPrivateMediaStorage
{
    private static readonly HttpClient Client = new();
    private readonly Uri _serviceUri;
    private readonly string _bucket;
    private readonly string _accessKey;
    private readonly string _secretKey;

    public S3PrivateMediaStorage(IOptions<ObjectStorageOptions> options)
    {
        var value = options.Value;
        _serviceUri = new Uri(value.ServiceUrl, UriKind.Absolute);
        _bucket = value.BucketName;
        _accessKey = value.AccessKey;
        _secretKey = value.SecretKey;
    }

    public async Task<long> GetLengthAsync(string storageKey, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Head, storageKey, null, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return 0;
        response.EnsureSuccessStatusCode();
        return response.Content.Headers.ContentLength ?? 0;
    }

    public async Task<string> GetSha256Async(string storageKey, CancellationToken cancellationToken)
    {
        var (stream, _, _) = await OpenReadAsync(storageKey, "application/octet-stream", "media", cancellationToken);
        await using (stream)
        {
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, storageKey, null, null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AppendAsync(string storageKey, long expectedOffset, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedOffset);
        var actualLength = await GetLengthAsync(storageKey, cancellationToken);
        if (actualLength == expectedOffset + content.Length)
        {
            var (persistedStream, _, _) = await OpenReadAsync(storageKey, "application/octet-stream", "media", cancellationToken);
            await using (persistedStream)
            {
                await using var persisted = new MemoryStream();
                await persistedStream.CopyToAsync(persisted, cancellationToken);
                if (persisted.GetBuffer().AsSpan((int)expectedOffset, content.Length).SequenceEqual(content.Span)) return;
            }
        }

        if (actualLength != expectedOffset)
            throw new InvalidOperationException($"Upload offset mismatch. Expected {expectedOffset} bytes, found {actualLength}.");

        await using var combined = new MemoryStream();
        if (actualLength > 0)
        {
            var (existing, _, _) = await OpenReadAsync(storageKey, "application/octet-stream", "media", cancellationToken);
            await using (existing) await existing.CopyToAsync(combined, cancellationToken);
        }
        await combined.WriteAsync(content, cancellationToken);
        await PutAsync(storageKey, combined.ToArray(), cancellationToken);
    }

    public async Task MoveAsync(string sourceStorageKey, string destinationStorageKey, CancellationToken cancellationToken)
    {
        ValidateKey(sourceStorageKey); ValidateKey(destinationStorageKey);
        using var copy = await SendAsync(HttpMethod.Put, destinationStorageKey, Array.Empty<byte>(),
            new Dictionary<string, string> { ["x-amz-copy-source"] = $"/{_bucket}/{sourceStorageKey}", ["x-amz-server-side-encryption"] = "AES256" }, cancellationToken);
        copy.EnsureSuccessStatusCode();
        using var delete = await SendAsync(HttpMethod.Delete, sourceStorageKey, null, null, cancellationToken);
        delete.EnsureSuccessStatusCode();
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> OpenReadAsync(string storageKey, string contentType, string fileName, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Get, storageKey, null, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) { response.Dispose(); throw new FileNotFoundException("Media object was not found.", storageKey); }
        response.EnsureSuccessStatusCode();
        // The response must remain alive until the caller disposes the stream.
        return (new ResponseStream(await response.Content.ReadAsStreamAsync(cancellationToken), response), contentType, fileName);
    }

    private async Task PutAsync(string storageKey, byte[] payload, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, storageKey, payload,
            new Dictionary<string, string> { ["x-amz-server-side-encryption"] = "AES256" }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string storageKey, byte[]? payload, IDictionary<string, string>? additionalHeaders, CancellationToken cancellationToken)
    {
        ValidateKey(storageKey);
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var path = $"/{_bucket}/{string.Join('/', storageKey.Split('/').Select(Uri.EscapeDataString))}";
        var payloadHash = Convert.ToHexString(SHA256.HashData(payload ?? Array.Empty<byte>())).ToLowerInvariant();
        var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = _serviceUri.Authority,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = amzDate
        };
        if (additionalHeaders is not null) foreach (var header in additionalHeaders) headers[header.Key] = header.Value;
        var canonicalHeaders = string.Concat(headers.Select(x => $"{x.Key}:{x.Value.Trim()}\n"));
        var signedHeaders = string.Join(';', headers.Keys);
        var canonicalRequest = $"{method.Method}\n{path}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var scope = $"{date}/us-east-1/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
        var signingKey = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4" + _secretKey), date), "us-east-1"), "s3"), "aws4_request");
        var signature = Hex(Hmac(signingKey, stringToSign));
        var request = new HttpRequestMessage(method, new Uri(_serviceUri, path));
        if (payload is not null) request.Content = new ByteArrayContent(payload);
        foreach (var header in headers.Where(x => x.Key != "host")) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        request.Headers.TryAddWithoutValidation("Authorization", $"AWS4-HMAC-SHA256 Credential={_accessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
        return await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static byte[] Hmac(byte[] key, string value) => new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(value));
    private static string Hex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();
    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("tenants/", StringComparison.Ordinal) || key.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Storage keys must be opaque tenant-scoped object keys.");
    }

    private sealed class ResponseStream(Stream inner, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => false;
        public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush(); public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
        public override int Read(byte[] b, int o, int c) => inner.Read(b, o, c); public override ValueTask<int> ReadAsync(Memory<byte> b, CancellationToken ct = default) => inner.ReadAsync(b, ct);
        public override long Seek(long o, SeekOrigin w) => inner.Seek(o, w); public override void SetLength(long v) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) { inner.Dispose(); response.Dispose(); } base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); response.Dispose(); await base.DisposeAsync(); }
    }
}
