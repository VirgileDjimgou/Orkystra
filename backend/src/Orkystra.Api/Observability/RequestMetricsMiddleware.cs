namespace Orkystra.Api.Observability;

public sealed class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public RequestMetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, RequestMetricsStore metricsStore)
    {
        await _next(context);
        metricsStore.Record(context.Response.StatusCode);
    }
}
