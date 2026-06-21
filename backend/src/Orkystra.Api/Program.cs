using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Orkystra.Api.Connectors;
using Orkystra.Api.ControlTower;
using Orkystra.Api.Observability;
using Orkystra.Api.Security;
using Orkystra.Api.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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
builder.Services.AddSingleton<ProviderCatalogService>();
builder.Services.AddSingleton<ControlTowerOverviewService>();
builder.Services.AddScoped<RequestTenantContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<RequestMetricsMiddleware>();
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

app.MapGet("/api/providers/catalog", async (ProviderCatalogService catalogService, CancellationToken cancellationToken) =>
{
    var catalog = await catalogService.BuildCatalogAsync(cancellationToken);
    return Results.Ok(catalog);
})
.RequireAuthorization()
.WithName("GetProviderCatalog");

app.Run();
