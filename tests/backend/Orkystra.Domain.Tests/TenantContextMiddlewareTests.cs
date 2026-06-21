using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orkystra.Api.Tenancy;

namespace Orkystra.Domain.Tests;

public sealed class TenantContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_skips_tenant_resolution_for_options_requests()
    {
        var nextWasCalled = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Options;
        context.Request.Path = "/api/control-tower/overview";

        var tenantContext = new RequestTenantContext();
        var resolutionService = new TenantResolutionService(Options.Create(new TenantAccessOptions
        {
            RequireTenantHeader = true,
            TenantHeaderName = "X-Tenant-Id"
        }));

        await middleware.InvokeAsync(context, tenantContext, resolutionService);

        Assert.True(nextWasCalled);
        Assert.Null(tenantContext.TenantId);
        Assert.NotEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }
}
