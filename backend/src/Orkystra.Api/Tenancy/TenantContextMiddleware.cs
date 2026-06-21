namespace Orkystra.Api.Tenancy;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, RequestTenantContext tenantContext, TenantResolutionService tenantResolutionService)
    {
        if (HttpMethods.IsOptions(context.Request.Method) ||
            context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/observability/metrics"))
        {
            await _next(context);
            return;
        }

        var resolution = tenantResolutionService.Resolve(context.Request.Headers);
        if (!resolution.Success || resolution.TenantId is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "tenant_resolution_failed",
                detail = resolution.FailureReason
            });
            return;
        }

        tenantContext.SetTenant(resolution.TenantId);
        context.Items[nameof(RequestTenantContext)] = resolution.TenantId;

        await _next(context);
    }
}
