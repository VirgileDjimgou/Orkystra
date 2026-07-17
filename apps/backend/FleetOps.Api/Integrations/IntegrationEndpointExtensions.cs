using System.Security.Claims;
using System.Text;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Integrations;

public static class IntegrationEndpointExtensions
{
    public static IEndpointRouteBuilder MapIntegrationAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        MapAdministrationGroup(app.MapGroup("/api/v1/admin/integrations"));
        var legacy = app.MapGroup("/api/admin/integrations")
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.Append("Deprecation", "true");
                context.HttpContext.Response.Headers.Append("Link", "</api/v1/admin/integrations>; rel=successor-version");
                return await next(context);
            });
        MapAdministrationGroup(legacy);

        return app;
    }

    private static void MapAdministrationGroup(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .RequireRateLimiting("integration-admin");

        group.MapGet("/api-keys", ListApiKeysAsync);
        group.MapPost("/api-keys", CreateApiKeyAsync);
        group.MapPost("/api-keys/{credentialId:guid}/revoke", RevokeApiKeyAsync);
        group.MapGet("/webhooks", ListWebhooksAsync);
        group.MapPost("/webhooks", CreateWebhookAsync);
        group.MapPost("/webhooks/{webhookId:guid}/disable", DisableWebhookAsync);
        group.MapGet("/contracts", ListContractsAsync);
        group.MapGet("/outbox", ListOutboxAsync);
        group.MapGet("/attempts", ListAttemptsAsync);
        group.MapGet("/sandbox-receipts", ListSandboxReceiptsAsync);

    }

    public static IEndpointRouteBuilder MapIntegrationPartnerEndpoints(this IEndpointRouteBuilder app)
    {
        var partner = app.MapGroup("/api/v1/integrations/partner")
            .RequireRateLimiting("integration-api-key");

        partner.MapGet("/fleet/vehicles", ListPartnerVehiclesAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
                Policy = "partner-fleet-read"
            });
        partner.MapGet("/webhooks/deliveries", ListPartnerWebhookDeliveriesAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
                Policy = "partner-webhook-read"
            });

        var device = app.MapGroup("/api/v1/integrations/device")
            .RequireRateLimiting("integration-api-key");
        device.MapPost("/tracking/events", IngestDeviceTelemetryAsync)
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
                Policy = "device-tracking-write"
            });

        return app;
    }

    public static IEndpointRouteBuilder MapIntegrationSandboxEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/integrations/sandbox/webhooks/{webhookId:guid}", ReceiveSandboxWebhookAsync);
        return app;
    }

    private static async Task<IResult> ListApiKeysAsync(
        HttpContext httpContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IApiKeyCredentialService apiKeyCredentialService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var credentials = await apiKeyCredentialService.ListAsync(tenant.OrganizationId, cancellationToken);
        return Results.Ok(credentials.Select(MapCredential).ToList());
    }

    private static async Task<IResult> CreateApiKeyAsync(
        CreateApiClientCredentialRequest request,
        HttpContext httpContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IApiKeyCredentialService apiKeyCredentialService,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (request.Scopes.Length == 0 || request.Scopes.Any(x => !IntegrationScope.All.Contains(x)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["scopes"] = [$"Scopes must be chosen from: {string.Join(", ", IntegrationScope.All)}."]
            });
        }

        var issued = await apiKeyCredentialService.CreateAsync(
            tenant.OrganizationId,
            request.Name,
            request.CredentialType,
            request.Scopes,
            cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "integration.api_key_created",
            "api-key",
            issued.Credential.Id.ToString(),
            new { issued.Credential.Name, issued.Credential.CredentialType, request.Scopes },
            cancellationToken);

        return Results.Created(
            $"/api/admin/integrations/api-keys/{issued.Credential.Id}",
            new CreatedApiClientCredentialResponse(
                issued.Credential.Id,
                issued.Credential.Name,
                issued.Credential.CredentialType,
                issued.Credential.GetScopes().ToArray(),
                issued.Credential.KeyId,
                issued.PlainTextSecret,
                issued.Credential.SecretPreview,
                issued.Credential.IsActive,
                issued.Credential.RowVersion));
    }

    private static async Task<IResult> RevokeApiKeyAsync(
        Guid credentialId,
        HttpContext httpContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IApiKeyCredentialService apiKeyCredentialService,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var credential = await apiKeyCredentialService.RevokeAsync(tenant.OrganizationId, credentialId, cancellationToken);
        if (credential is null)
        {
            return Results.NotFound();
        }

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "integration.api_key_revoked",
            "api-key",
            credential.Id.ToString(),
            new { credential.Name, credential.KeyId },
            cancellationToken);

        return Results.Ok(MapCredential(credential));
    }

    private static async Task<IResult> ListWebhooksAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var webhooks = await dbContext.WebhookEndpoints
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.EventType)
            .ToListAsync(cancellationToken);
        return Results.Ok(webhooks.Select(MapWebhook).ToList());
    }

    private static async Task<IResult> CreateWebhookAsync(
        CreateWebhookEndpointRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (!IntegrationEventType.All.Contains(request.EventType))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["eventType"] = [$"Event type must be chosen from: {string.Join(", ", IntegrationEventType.All)}."]
            });
        }

        var webhookId = Guid.NewGuid();
        var targetUrl = request.IsSandbox
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/v1/integrations/sandbox/webhooks/{webhookId}"
            : request.TargetUrl ?? string.Empty;

        WebhookEndpoint endpoint;
        try
        {
            endpoint = new WebhookEndpoint(
                tenant.OrganizationId,
                request.Name,
                request.EventType,
                targetUrl,
                request.SigningSecret,
                request.IsSandbox,
                webhookId);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            var key = ex is ArgumentException argumentException ? argumentException.ParamName ?? "body" : "body";
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [key] = [ex.Message]
            });
        }

        dbContext.WebhookEndpoints.Add(endpoint);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "integration.webhook_created",
            "webhook",
            endpoint.Id.ToString(),
            new { endpoint.Name, endpoint.EventType, endpoint.TargetUrl, endpoint.IsSandbox },
            cancellationToken);

        return Results.Created($"/api/admin/integrations/webhooks/{endpoint.Id}", MapWebhook(endpoint));
    }

    private static async Task<IResult> DisableWebhookAsync(
        Guid webhookId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var endpoint = await dbContext.WebhookEndpoints
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == webhookId, cancellationToken);
        if (endpoint is null)
        {
            return Results.NotFound();
        }

        try
        {
            endpoint.Disable(timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["state"] = [ex.Message]
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "integration.webhook_disabled",
            "webhook",
            endpoint.Id.ToString(),
            new { endpoint.Name, endpoint.EventType },
            cancellationToken);

        return Results.Ok(MapWebhook(endpoint));
    }

    private static IResult ListContractsAsync()
    {
        var contracts = new[]
        {
            new IntegrationContractResponse(
                IntegrationEventType.FleetVehicleCreated,
                "Raised when a vehicle is created by an administrator import or form flow.",
                new
                {
                    vehicleId = Guid.Empty,
                    registrationNumber = "NW-100",
                    displayName = "Dispatch van",
                    isActive = true,
                    currentOdometerKm = 0
                }),
            new IntegrationContractResponse(
                IntegrationEventType.MissionStatusChanged,
                "Raised when a mission transitions to another workflow status.",
                new
                {
                    missionId = Guid.Empty,
                    reference = "NW-M-100",
                    status = "Assigned",
                    driverId = Guid.Empty,
                    vehicleId = Guid.Empty
                }),
            new IntegrationContractResponse(
                IntegrationEventType.AlertOpened,
                "Raised when a new operational alert is opened for a tenant.",
                new
                {
                    alertId = Guid.Empty,
                    ruleType = "VehicleInactive",
                    severity = "Warning",
                    title = "Inactive vehicle"
                })
        };

        return Results.Ok(contracts);
    }

    private static async Task<IResult> ListOutboxAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var items = await dbContext.IntegrationOutboxMessages
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(100)
            .Select(x => new IntegrationOutboxMessageResponse(
                x.Id,
                x.WebhookEndpointId,
                x.EventType,
                x.AggregateType,
                x.AggregateId,
                x.Status,
                x.AttemptCount,
                x.OccurredAtUtc,
                x.NextAttemptAtUtc,
                x.DeliveredAtUtc,
                x.DeadLetteredAtUtc,
                x.LastError))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> ListAttemptsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var attempts = await dbContext.WebhookDeliveryAttempts
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.AttemptedAtUtc)
            .Take(100)
            .Select(x => new WebhookDeliveryAttemptResponse(
                x.Id,
                x.OutboxMessageId,
                x.WebhookEndpointId,
                x.AttemptNumber,
                x.ResponseStatusCode,
                x.ResponseBody,
                x.IsSuccess,
                x.AttemptedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(attempts);
    }

    private static async Task<IResult> ListSandboxReceiptsAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var receipts = await dbContext.SandboxWebhookReceipts
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Take(100)
            .Select(x => new SandboxWebhookReceiptResponse(
                x.Id,
                x.WebhookEndpointId,
                x.EventType,
                x.Signature,
                x.PayloadJson,
                x.ReceivedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(receipts);
    }

    private static async Task<IResult> ListPartnerVehiclesAsync(
        ClaimsPrincipal user,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!IsApiKeyType(user, ApiClientCredentialType.Partner, out var organizationId))
        {
            return Results.Forbid();
        }

        var vehicles = await dbContext.Vehicles
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.RegistrationNumber)
            .Select(x => new PartnerVehicleExportResponse(
                x.Id,
                x.RegistrationNumber,
                x.DisplayName,
                x.IsActive,
                x.CurrentOdometerKm))
            .ToListAsync(cancellationToken);

        return Results.Ok(vehicles);
    }

    private static async Task<IResult> ListPartnerWebhookDeliveriesAsync(
        ClaimsPrincipal user,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!IsApiKeyType(user, ApiClientCredentialType.Partner, out var organizationId))
        {
            return Results.Forbid();
        }

        var deliveries = await dbContext.IntegrationOutboxMessages
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(50)
            .Select(x => new IntegrationOutboxMessageResponse(
                x.Id,
                x.WebhookEndpointId,
                x.EventType,
                x.AggregateType,
                x.AggregateId,
                x.Status,
                x.AttemptCount,
                x.OccurredAtUtc,
                x.NextAttemptAtUtc,
                x.DeliveredAtUtc,
                x.DeadLetteredAtUtc,
                x.LastError))
            .ToListAsync(cancellationToken);

        return Results.Ok(deliveries);
    }

    private static async Task<IResult> IngestDeviceTelemetryAsync(
        DeviceTelemetryIngestionRequest request,
        ClaimsPrincipal user,
        Tracking.TrackingIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        if (!IsApiKeyType(user, ApiClientCredentialType.Device, out var organizationId))
        {
            return Results.Forbid();
        }

        try
        {
            var response = await ingestionService.IngestAsync(
                new Tracking.IngestTelemetryRequest(
                    organizationId,
                    request.VehicleId,
                    request.DeviceId,
                    request.EventId,
                    request.RecordedAtUtc,
                    request.Latitude,
                    request.Longitude,
                    request.SpeedKph,
                    request.HeadingDegrees),
                cancellationToken);
            return Results.Accepted("/api/v1/tracking/positions", response);
        }
        catch (Tracking.TrackingValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.Field] = [ex.Message]
            });
        }
        catch (ArgumentException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "body"] = [ex.Message]
            });
        }
    }

    private static async Task<IResult> ReceiveSandboxWebhookAsync(
        Guid webhookId,
        HttpRequest request,
        FleetOpsDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.WebhookEndpoints
            .FirstOrDefaultAsync(x => x.Id == webhookId && x.IsSandbox, cancellationToken);
        if (endpoint is null)
        {
            return Results.NotFound();
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = request.Headers["X-FleetOps-Signature"].ToString();
        var expected = WebhookDispatchService.ComputeSignature(endpoint.SigningSecret, payload);
        if (!string.Equals(signature, expected, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        var eventType = request.Headers["X-FleetOps-Event"].ToString();
        dbContext.SandboxWebhookReceipts.Add(new SandboxWebhookReceipt(
            endpoint.OrganizationId,
            endpoint.Id,
            eventType,
            signature,
            payload,
            timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Accepted();
    }

    private static bool IsApiKeyType(
        ClaimsPrincipal user,
        ApiClientCredentialType credentialType,
        out Guid organizationId)
    {
        organizationId = Guid.Empty;
        if (!Guid.TryParse(user.FindFirstValue(TenantClaimTypes.OrganizationId), out organizationId))
        {
            return false;
        }

        var type = user.FindFirstValue("api_key_type");
        return string.Equals(type, credentialType.ToString(), StringComparison.Ordinal);
    }

    private static ApiClientCredentialResponse MapCredential(ApiClientCredential credential) => new(
        credential.Id,
        credential.Name,
        credential.CredentialType,
        credential.GetScopes().ToArray(),
        credential.KeyId,
        credential.SecretPreview,
        credential.IsActive,
        credential.LastUsedAtUtc,
        credential.RevokedAtUtc,
        credential.RowVersion);

    private static WebhookEndpointResponse MapWebhook(WebhookEndpoint endpoint) => new(
        endpoint.Id,
        endpoint.Name,
        endpoint.EventType,
        endpoint.TargetUrl,
        endpoint.IsActive,
        endpoint.IsSandbox,
        endpoint.LastSucceededAtUtc,
        endpoint.DisabledAtUtc,
        endpoint.RowVersion);
}
