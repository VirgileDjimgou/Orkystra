using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Orkystra.Api.AI;
using Orkystra.Api.Connectors;
using Orkystra.Api.ControlTower;
using Orkystra.Api.Observability;
using Orkystra.Api.Persistence;
using Orkystra.Api.Optimization;
using Orkystra.Contracts.Ai;
using Orkystra.Contracts.Connectors;
using Orkystra.Contracts.ControlTower;
using Orkystra.Contracts.Optimization;
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
            .WithMethods("GET", "POST", "PUT");
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
builder.Services.Configure<AiServiceOptions>(builder.Configuration.GetSection(AiServiceOptions.SectionName));
builder.Services.Configure<OptimizationServiceOptions>(builder.Configuration.GetSection(OptimizationServiceOptions.SectionName));
builder.Services.Configure<OperationalPersistenceOptions>(builder.Configuration.GetSection(OperationalPersistenceOptions.SectionName));

builder.Services.AddSingleton<ApiKeyValidator>();
builder.Services.AddSingleton<TenantResolutionService>();
builder.Services.AddSingleton<RequestMetricsStore>();
builder.Services.AddSingleton<IAuditStore, FileAuditStore>();
builder.Services.AddSingleton(provider => new ProviderRuntimeStore(
    provider.GetRequiredService<IOptions<ProviderRuntimeOptions>>(),
    Path.Combine(builder.Environment.ContentRootPath, "appsettings.Local.json")));
builder.Services.AddSingleton(provider => new OperationalPersistenceStore(
    provider.GetRequiredService<IOptions<OperationalPersistenceOptions>>(),
    builder.Environment.ContentRootPath));
builder.Services.AddHttpClient("provider-rest-transport", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<AiWorkflowService>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<AiServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddHttpClient<RouteOptimizationWorkflowService>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<OptimizationServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddSingleton<ProviderRegistryFactory>();
builder.Services.AddSingleton<ProviderCatalogService>();
builder.Services.AddSingleton<ControlTowerOverviewService>();
builder.Services.AddSingleton<WarehouseProjectionService>();
builder.Services.AddSingleton<TransportProjectionService>();
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

app.MapGet("/observability/persistence/projections", async (
    RequestTenantContext tenantContext,
    OperationalPersistenceStore persistenceStore,
    IOptions<OperationalPersistenceOptions> options,
    string? projectionName,
    int? count,
    CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var boundedCount = Math.Clamp(count ?? 20, 1, options.Value.ReadLimit);
    var entries = await persistenceStore.ReadProjectionSnapshotsAsync(tenantId, projectionName, boundedCount, cancellationToken);

    return Results.Ok(new
    {
        tenantId,
        count = entries.Count,
        entries = entries.Select(entry => new
        {
            entry.TenantId,
            entry.ProjectionName,
            entry.ProjectionKey,
            entry.Source,
            entry.CapturedAtUtc,
            payload = JsonDocument.Parse(entry.PayloadJson).RootElement.Clone()
        })
    });
})
.RequireAuthorization()
.WithName("GetPersistedProjectionSnapshots");

app.MapGet("/observability/persistence/workflows", async (
    RequestTenantContext tenantContext,
    OperationalPersistenceStore persistenceStore,
    IOptions<OperationalPersistenceOptions> options,
    string? workflowKind,
    int? count,
    CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var boundedCount = Math.Clamp(count ?? 20, 1, options.Value.ReadLimit);
    var entries = await persistenceStore.ReadWorkflowRunsAsync(tenantId, workflowKind, boundedCount, cancellationToken);

    return Results.Ok(new
    {
        tenantId,
        count = entries.Count,
        entries = entries.Select(entry => new
        {
            entry.RunId,
            entry.TenantId,
            entry.WorkflowKind,
            entry.SubjectKey,
            entry.ScenarioId,
            entry.Source,
            entry.Status,
            entry.CreatedAtUtc,
            payload = JsonDocument.Parse(entry.PayloadJson).RootElement.Clone()
        })
    });
})
.RequireAuthorization()
.WithName("GetPersistedWorkflowRuns");

app.MapGet("/api/control-tower/overview", async (
    RequestTenantContext tenantContext,
    ControlTowerOverviewService overviewService,
    OperationalPersistenceStore persistenceStore,
    CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var overview = await overviewService.BuildOverviewAsync(tenantId, cancellationToken);
    await persistenceStore.UpsertProjectionAsync(tenantId, "control-tower-overview", "current", "api", overview, cancellationToken);
    return Results.Ok(overview);
})
.RequireAuthorization()
.WithName("GetControlTowerOverview");

app.MapGet("/api/warehouses", async (
    RequestTenantContext tenantContext,
    WarehouseProjectionService warehouseProjectionService,
    OperationalPersistenceStore persistenceStore,
    CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var warehouses = await warehouseProjectionService.ListAsync(cancellationToken);
    await persistenceStore.UpsertProjectionAsync(tenantId, "warehouse-summaries", "all", "api", warehouses, cancellationToken);
    return Results.Ok(warehouses);
})
.RequireAuthorization()
.WithName("ListWarehouseProjections");

app.MapGet("/api/warehouses/{warehouseId:guid}", async (
    Guid warehouseId,
    RequestTenantContext tenantContext,
    WarehouseProjectionService warehouseProjectionService,
    OperationalPersistenceStore persistenceStore,
    CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var warehouse = await warehouseProjectionService.GetByIdAsync(warehouseId, cancellationToken);
    if (warehouse is null)
    {
        return Results.NotFound();
    }

    await persistenceStore.UpsertProjectionAsync(tenantId, "warehouse-detail", warehouseId.ToString("D"), "api", warehouse, cancellationToken);
    return Results.Ok(warehouse);
})
.RequireAuthorization()
.WithName("GetWarehouseProjection");

app.MapGet("/api/providers/catalog", async (
    RequestTenantContext tenantContext,
    ProviderCatalogService catalogService,
    OperationalPersistenceStore persistenceStore,
    CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var catalog = await catalogService.BuildCatalogAsync(cancellationToken);
    await persistenceStore.UpsertProjectionAsync(tenantId, "provider-catalog", "current", "api", catalog, cancellationToken);
    return Results.Ok(catalog);
})
.RequireAuthorization()
.WithName("GetProviderCatalog");

app.MapGet("/api/transport/routes", async (
    RequestTenantContext tenantContext,
    TransportProjectionService transportProjectionService,
    OperationalPersistenceStore persistenceStore,
    CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var routes = await transportProjectionService.ListAsync(cancellationToken);
    await persistenceStore.UpsertProjectionAsync(tenantId, "route-summaries", "all", "api", routes, cancellationToken);
    return Results.Ok(routes);
})
.RequireAuthorization()
.WithName("ListTransportProjections");

app.MapGet("/api/transport/routes/{routeId:guid}", async (
    Guid routeId,
    RequestTenantContext tenantContext,
    TransportProjectionService transportProjectionService,
    OperationalPersistenceStore persistenceStore,
    CancellationToken cancellationToken) =>
{
    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var route = await transportProjectionService.GetByIdAsync(routeId, cancellationToken);
    if (route is null)
    {
        return Results.NotFound();
    }

    await persistenceStore.UpsertProjectionAsync(tenantId, "route-detail", routeId.ToString("D"), "api", route, cancellationToken);
    return Results.Ok(route);
})
.RequireAuthorization()
.WithName("GetTransportProjection");

app.MapPost("/api/transport/routes/{routeId:guid}/optimization", async (
    Guid routeId,
    RouteOptimizationRunRequest request,
    RequestTenantContext tenantContext,
    TransportProjectionService transportProjectionService,
    RouteOptimizationWorkflowService optimizationWorkflowService,
    OperationalPersistenceStore persistenceStore,
    CancellationToken cancellationToken) =>
{
    var route = await transportProjectionService.GetByIdAsync(routeId, cancellationToken);
    if (route is null)
    {
        return Results.NotFound();
    }

    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var workflowResult = await optimizationWorkflowService.BuildOptimizationAsync(tenantId, route, request, cancellationToken);
    await persistenceStore.AppendWorkflowRunAsync(
        tenantId,
        "route-optimization",
        routeId.ToString("D"),
        request.ScenarioId,
        workflowResult.Source,
        workflowResult.Optimization.Status,
        workflowResult,
        cancellationToken);
    return Results.Ok(workflowResult);
})
.RequireAuthorization()
.WithName("CreateRouteOptimization");

app.MapPost("/api/ai/recommendations", async (
    AiRecommendationQueryRequest request,
    RequestTenantContext tenantContext,
    ControlTowerOverviewService overviewService,
    AiWorkflowService aiWorkflowService,
    OperationalPersistenceStore persistenceStore,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["question"] = ["A question is required."]
        });
    }

    var tenantId = tenantContext.TenantId ?? "local-demo-tenant";
    var overview = await overviewService.BuildOverviewAsync(tenantId, cancellationToken);
    var workflowResult = await aiWorkflowService.BuildRecommendationAsync(tenantId, request, overview, cancellationToken);
    await persistenceStore.AppendWorkflowRunAsync(
        tenantId,
        "ai-recommendation",
        "control-tower",
        request.ScenarioId,
        workflowResult.Source,
        "completed",
        workflowResult,
        cancellationToken);
    return Results.Ok(workflowResult);
})
.RequireAuthorization()
.WithName("CreateAiRecommendation");

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
