using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Orkystra.Api.Connectors;
using Orkystra.Api.ControlTower;
using Orkystra.Api.Observability;
using Orkystra.Contracts.Connectors;
using Orkystra.Api.Security;
using Orkystra.Api.Tenancy;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend-dev", policy =>
    {
        policy
            .WithOrigins(
                "http://127.0.0.1:4173",
                "http://localhost:4173",
                "http://127.0.0.1:5173",
                "http://localhost:5173")
            .WithHeaders("Content-Type", "X-Api-Key", "X-Tenant-Id")
            .WithMethods("GET", "PUT");
    });
});
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();

builder.Services.Configure<ApiSecurityOptions>(builder.Configuration.GetSection(ApiSecurityOptions.SectionName));
builder.Services.Configure<TenantAccessOptions>(builder.Configuration.GetSection(TenantAccessOptions.SectionName));
builder.Services.Configure<ObservabilityOptions>(builder.Configuration.GetSection(ObservabilityOptions.SectionName));
builder.Services.Configure<ProviderRuntimeOptions>(builder.Configuration.GetSection(ProviderRuntimeOptions.SectionName));

builder.Services.AddSingleton<ApiKeyValidator>();
builder.Services.AddSingleton<TenantResolutionService>();
builder.Services.AddSingleton<RequestMetricsStore>();
builder.Services.AddSingleton<IAuditStore, FileAuditStore>();
builder.Services.AddSingleton(provider => new ProviderRuntimeStore(
    provider.GetRequiredService<IOptions<ProviderRuntimeOptions>>(),
    Path.Combine(builder.Environment.ContentRootPath, "appsettings.Local.json")));
builder.Services.AddSingleton<ProviderCatalogService>();
builder.Services.AddSingleton<ControlTowerOverviewService>();
builder.Services.AddSingleton<WarehouseProjectionService>();
builder.Services.AddScoped<RequestTenantContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<RequestMetricsMiddleware>();
app.UseCors("frontend-dev");
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLoggingMiddleware>();

app.MapGet("/health/live", () => Results.Ok(new
{
    service = "orkystra-api",
    status = "healthy"
}))
.AllowAnonymous()
.WithName("GetLiveness");

app.MapGet("/health/ready", (IConfiguration configuration) => Results.Ok(new
{
    service = "orkystra-api",
    status = "ready",
    dependencies = new
    {
        postgres = configuration["Dependencies:Postgres"] ?? "configured",
        mqtt = configuration["Dependencies:Mqtt"] ?? "configured",
        qdrant = configuration["Dependencies:Qdrant"] ?? "configured"
    }
}))
.AllowAnonymous()
.WithName("GetReadiness");

app.MapGet("/observability/metrics", (RequestMetricsStore metricsStore) => Results.Ok(metricsStore.Snapshot()))
.AllowAnonymous()
.WithName("GetMetrics");

app.MapGet("/observability/context", (HttpContext httpContext, RequestTenantContext tenantContext) => Results.Ok(new
{
    user = httpContext.User.Identity?.Name ?? "anonymous",
    tenantId = tenantContext.TenantId,
    correlationId = httpContext.Response.Headers["X-Correlation-Id"].ToString(),
    environment = app.Environment.EnvironmentName
}))
.RequireAuthorization()
.WithName("GetOperationalContext");

app.MapGet("/observability/audit", async (IAuditStore auditStore, IOptions<ObservabilityOptions> options, int? count, CancellationToken cancellationToken) =>
{
    var requestedCount = count ?? 50;
    var boundedCount = Math.Clamp(requestedCount, 1, options.Value.AuditReadLimit);
    var entries = await auditStore.ReadRecentAsync(boundedCount, cancellationToken);
    return Results.Ok(new
    {
        count = entries.Count,
        entries
    });
})
.RequireAuthorization()
.WithName("GetRecentAuditEntries");

app.MapGet("/api/control-tower/overview", async (RequestTenantContext tenantContext, ControlTowerOverviewService overviewService, CancellationToken cancellationToken) =>
{
    var overview = await overviewService.BuildOverviewAsync(tenantContext.TenantId ?? "local-demo-tenant", cancellationToken);
    return Results.Ok(overview);
})
.RequireAuthorization()
.WithName("GetControlTowerOverview");

app.MapGet("/api/warehouses", async (WarehouseProjectionService warehouseProjectionService, CancellationToken cancellationToken) =>
{
    var warehouses = await warehouseProjectionService.ListAsync(cancellationToken);
    return Results.Ok(warehouses);
})
.RequireAuthorization()
.WithName("ListWarehouseProjections");

app.MapGet("/api/warehouses/{warehouseId:guid}", async (Guid warehouseId, WarehouseProjectionService warehouseProjectionService, CancellationToken cancellationToken) =>
{
    var warehouse = await warehouseProjectionService.GetByIdAsync(warehouseId, cancellationToken);
    return warehouse is null ? Results.NotFound() : Results.Ok(warehouse);
})
.RequireAuthorization()
.WithName("GetWarehouseProjection");

app.MapGet("/api/providers/catalog", async (ProviderCatalogService catalogService, CancellationToken cancellationToken) =>
{
    var catalog = await catalogService.BuildCatalogAsync(cancellationToken);
    return Results.Ok(catalog);
})
.RequireAuthorization()
.WithName("GetProviderCatalog");

app.MapPut("/api/providers/catalog/{providerId}/configuration", async (
    string providerId,
    UpdateProviderConfigurationRequest request,
    ProviderRuntimeStore runtimeStore,
    CancellationToken cancellationToken) =>
{
    try
    {
        await runtimeStore.UpdateAsync(providerId, request, cancellationToken);
        return Results.NoContent();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new
        {
            message = exception.Message
        });
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["configuration"] = [exception.Message]
        });
    }
})
.RequireAuthorization()
.WithName("UpdateProviderConfiguration");

app.Run();
