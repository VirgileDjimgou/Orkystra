using Microsoft.Extensions.Options;
using Orkystra.Api.Tenancy;

namespace Orkystra.Api.Observability;

public sealed class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private readonly ObservabilityOptions _options;
    private readonly IAuditStore _auditStore;

    public AuditLoggingMiddleware(
        RequestDelegate next,
        ILogger<AuditLoggingMiddleware> logger,
        IOptions<ObservabilityOptions> options,
        IAuditStore auditStore)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _auditStore = auditStore;
    }

    public async Task InvokeAsync(HttpContext context, RequestTenantContext tenantContext)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        await _next(context);

        if (!_options.EnableAuditLogging || context.Request.Path.StartsWithSegments("/health"))
        {
            return;
        }

        var entry = new AuditEntry(
            context.User.Identity?.Name ?? "anonymous",
            context.Request.Method,
            context.Request.Path,
            DateTimeOffset.UtcNow,
            tenantContext.TenantId ?? "n/a",
            context.Request.Headers["X-Orkystra-Reason"].ToString(),
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            correlationId,
            context.Response.StatusCode);

        _logger.LogInformation(
            "AUDIT who={User} what={Method} {Path} when={Timestamp} tenant={TenantId} why={Reason} from={RemoteIp} correlationId={CorrelationId} status={StatusCode}",
            entry.User,
            entry.Method,
            entry.Path,
            entry.TimestampUtc,
            entry.TenantId,
            entry.Reason,
            entry.RemoteIp,
            entry.CorrelationId,
            entry.StatusCode);

        await _auditStore.AppendAsync(entry, context.RequestAborted);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        const string HeaderName = "X-Correlation-Id";
        if (context.Request.Headers.TryGetValue(HeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        return Guid.NewGuid().ToString("D");
    }
}
