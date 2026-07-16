using System.Text.Json;
using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Api.Tracking;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Admin;

public static class DataLifecycleEndpointExtensions
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly DataLifecycleCategoryResponse[] SupportedCategories =
    [
        new(
            "tracking-history",
            "Tracking history",
            "Delete telemetry history points and stale current positions older than the cutoff."),
        new(
            "integration-history",
            "Integration history",
            "Delete sandbox receipts, webhook attempts, and delivered or dead-letter outbox rows older than the cutoff."),
        new(
            "upload-sessions",
            "Upload sessions",
            "Delete expired media upload session rows older than the cutoff.")
    ];

    public static IEndpointRouteBuilder MapDataLifecycleAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/data-lifecycle")
            .RequireAuthorization(new AuthorizeAttribute { Roles = SystemRoles.Admin });

        group.MapGet("/summary", GetSummaryAsync);
        group.MapGet("/export", ExportTenantSnapshotAsync);
        group.MapPost("/purge", PurgeAsync);

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IOptions<TrackingOptions> trackingOptions,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(x => x.Id == tenant.OrganizationId, cancellationToken);

        var counts = new List<DataLifecycleCountResponse>
        {
            new("users", "Users", await dbContext.Users.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken)),
            new("vehicles", "Vehicles", await dbContext.Vehicles.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken)),
            new("drivers", "Drivers", await dbContext.Drivers.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken)),
            new("devices", "Devices", await dbContext.GpsDevices.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken)),
            new("telemetry-points", "Telemetry points", await dbContext.TelemetryPoints.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken)),
            new("audit-logs", "Audit logs", await dbContext.AuditLogs.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken)),
            new("delivery-proofs", "Delivery proofs", await dbContext.DeliveryProofs.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken)),
            new("webhook-attempts", "Webhook attempts", await dbContext.WebhookDeliveryAttempts.CountAsync(x => x.OrganizationId == tenant.OrganizationId, cancellationToken))
        };

        return Results.Ok(new DataLifecycleSummaryResponse(
            DateTimeOffset.UtcNow,
            organization.Name,
            organization.Slug,
            Math.Max(1, trackingOptions.Value.RetentionDays),
            counts,
            SupportedCategories));
    }

    private static async Task<IResult> ExportTenantSnapshotAsync(
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IOptions<TrackingOptions> trackingOptions,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(x => x.Id == tenant.OrganizationId, cancellationToken);
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var userRoleLookup = new Dictionary<Guid, string[]>(users.Count);
        foreach (var user in users)
        {
            var roleNames =
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == user.Id
                select role.Name!;
            userRoleLookup[user.Id] = await roleNames.OrderBy(x => x).ToArrayAsync(cancellationToken);
        }

        var snapshot = new
        {
            schemaVersion = "pilot-1",
            exportedAtUtc = DateTimeOffset.UtcNow,
            retention = new
            {
                trackingDays = Math.Max(1, trackingOptions.Value.RetentionDays)
            },
            organization = new
            {
                organization.Id,
                organization.Name,
                organization.Slug
            },
            users = users.Select(user => new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.IsActive,
                user.DriverId,
                user.TwoFactorEnabled,
                roles = userRoleLookup[user.Id]
            }),
            fleet = new
            {
                vehicles = await dbContext.Vehicles.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.RegistrationNumber)
                    .Select(x => new
                    {
                        x.Id,
                        x.RegistrationNumber,
                        x.DisplayName,
                        x.IsActive,
                        x.CurrentOdometerKm,
                        x.RowVersion
                    })
                    .ToListAsync(cancellationToken),
                drivers = await dbContext.Drivers.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.FullName)
                    .Select(x => new
                    {
                        x.Id,
                        x.FullName,
                        x.LicenseNumber,
                        x.PhoneNumber,
                        x.IsActive,
                        x.RowVersion
                    })
                    .ToListAsync(cancellationToken),
                devices = await dbContext.GpsDevices.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.SerialNumber)
                    .Select(x => new
                    {
                        x.Id,
                        x.SerialNumber,
                        x.DisplayName,
                        x.IsActive,
                        x.RowVersion
                    })
                    .ToListAsync(cancellationToken),
                assignments = await dbContext.DeviceAssignments.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.AssignedAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.DeviceId,
                        x.VehicleId,
                        x.AssignedAtUtc,
                        x.UnassignedAtUtc
                    })
                    .ToListAsync(cancellationToken)
            },
            tracking = new
            {
                currentPositions = await dbContext.CurrentVehiclePositions.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.RecordedAtUtc)
                    .Select(x => new
                    {
                        x.VehicleId,
                        x.DeviceId,
                        x.EventId,
                        x.RecordedAtUtc,
                        x.Latitude,
                        x.Longitude,
                        x.SpeedKph,
                        x.HeadingDegrees
                    })
                    .ToListAsync(cancellationToken),
                telemetryPoints = await dbContext.TelemetryPoints.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.RecordedAtUtc)
                    .Select(x => new
                    {
                        x.VehicleId,
                        x.DeviceId,
                        x.EventId,
                        x.RecordedAtUtc,
                        x.IngestedAtUtc,
                        x.Latitude,
                        x.Longitude,
                        x.SpeedKph,
                        x.HeadingDegrees
                    })
                    .ToListAsync(cancellationToken)
            },
            dispatch = new
            {
                missions = await dbContext.Missions.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.ScheduledStartUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.Reference,
                        x.Title,
                        x.Status,
                        x.DriverId,
                        x.VehicleId,
                        x.ScheduledStartUtc,
                        x.ScheduledEndUtc,
                        x.RowVersion
                    })
                    .ToListAsync(cancellationToken),
                missionStops = await dbContext.MissionStops.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.MissionId)
                    .ThenBy(x => x.Sequence)
                    .Select(x => new
                    {
                        x.Id,
                        x.MissionId,
                        x.Sequence,
                        x.Name,
                        x.Address,
                        x.PlannedArrivalUtc
                    })
                    .ToListAsync(cancellationToken),
                timeline = await dbContext.MissionTimelineEvents.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.MissionId,
                        x.EventType,
                        x.Description,
                        x.OccurredAtUtc
                    })
                    .ToListAsync(cancellationToken)
            },
            fieldOperations = new
            {
                inspections = await dbContext.PreDepartureInspections.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.CreatedAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.MissionId,
                        x.DriverId,
                        x.Outcome,
                        x.Notes,
                        x.CreatedAtUtc,
                        x.CompletedAtUtc
                    })
                    .ToListAsync(cancellationToken),
                inspectionItems = await dbContext.InspectionItemResults.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.InspectionId)
                    .ThenBy(x => x.Sequence)
                    .Select(x => new
                    {
                        x.Id,
                        x.InspectionId,
                        x.Sequence,
                        x.Code,
                        x.Label,
                        x.IsPass,
                        x.DefectSeverity,
                        x.Notes,
                        x.PhotoAssetId
                    })
                    .ToListAsync(cancellationToken),
                deliveryProofs = await dbContext.DeliveryProofs.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.DeliveredAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.MissionId,
                        x.MissionStopId,
                        x.RecipientName,
                        x.SignatureName,
                        x.Notes,
                        x.DeliveredAtUtc
                    })
                    .ToListAsync(cancellationToken),
                deliveryProofPhotos = await dbContext.DeliveryProofPhotos.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.DeliveryProofId)
                    .Select(x => new
                    {
                        x.Id,
                        x.DeliveryProofId,
                        x.MediaAssetId,
                        x.Caption
                    })
                    .ToListAsync(cancellationToken),
                mediaAssets = await dbContext.MediaAssets.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.FileName)
                    .Select(x => new
                    {
                        x.Id,
                        x.FileName,
                        x.ContentType,
                        x.SizeBytes,
                        x.StorageKey
                    })
                    .ToListAsync(cancellationToken),
                uploadSessions = await dbContext.MediaUploadSessions.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.ExpiresAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.DriverId,
                        x.Purpose,
                        x.FileName,
                        x.ContentType,
                        x.TotalBytes,
                        x.UploadedBytes,
                        x.ExpiresAtUtc,
                        x.IsCompleted,
                        x.MediaAssetId
                    })
                    .ToListAsync(cancellationToken)
            },
            alerts = new
            {
                alerts = await dbContext.OperationalAlerts.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.LastDetectedAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.RuleType,
                        x.Severity,
                        x.Status,
                        x.Title,
                        x.Message,
                        x.TargetType,
                        x.TargetEntityId,
                        x.AssignedToUserId,
                        x.AssignedToDisplayName,
                        x.AcknowledgedByUserId,
                        x.AcknowledgedByDisplayName,
                        x.LastDetectedAtUtc,
                        x.AssignedAtUtc,
                        x.AcknowledgedAtUtc,
                        x.ResolvedAtUtc,
                        x.RowVersion
                    })
                    .ToListAsync(cancellationToken),
                notifications = await dbContext.AlertNotifications.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.SentAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.AlertId,
                        x.Channel,
                        x.Subject,
                        x.Body,
                        x.SentAtUtc
                    })
                    .ToListAsync(cancellationToken)
            },
            integrations = new
            {
                apiCredentials = await dbContext.ApiClientCredentials.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.Name)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.CredentialType,
                        scopes = x.ScopeList,
                        x.KeyId,
                        x.SecretPreview,
                        x.IsActive,
                        x.LastUsedAtUtc,
                        x.RevokedAtUtc
                    })
                    .ToListAsync(cancellationToken),
                webhooks = await dbContext.WebhookEndpoints.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.Name)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.EventType,
                        x.TargetUrl,
                        x.IsActive,
                        x.IsSandbox,
                        x.LastSucceededAtUtc,
                        x.DisabledAtUtc
                    })
                    .ToListAsync(cancellationToken),
                outbox = await dbContext.IntegrationOutboxMessages.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Select(x => new
                    {
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
                        x.LastError
                    })
                    .ToListAsync(cancellationToken),
                deliveryAttempts = await dbContext.WebhookDeliveryAttempts.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.AttemptedAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.OutboxMessageId,
                        x.WebhookEndpointId,
                        x.AttemptNumber,
                        x.ResponseStatusCode,
                        x.IsSuccess,
                        x.AttemptedAtUtc
                    })
                    .ToListAsync(cancellationToken),
                sandboxReceipts = await dbContext.SandboxWebhookReceipts.AsNoTracking()
                    .Where(x => x.OrganizationId == tenant.OrganizationId)
                    .OrderBy(x => x.ReceivedAtUtc)
                    .Select(x => new
                    {
                        x.Id,
                        x.WebhookEndpointId,
                        x.EventType,
                        x.ReceivedAtUtc
                    })
                    .ToListAsync(cancellationToken)
            },
            audit = await dbContext.AuditLogs.AsNoTracking()
                .Where(x => x.OrganizationId == tenant.OrganizationId)
                .OrderBy(x => x.OccurredAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.ActorUserId,
                    x.ActionType,
                    x.TargetType,
                    x.TargetId,
                    x.Metadata,
                    x.OccurredAtUtc
                })
                .ToListAsync(cancellationToken)
        };

        return Results.Text(
            JsonSerializer.Serialize(snapshot, ExportJsonOptions),
            "application/json");
    }

    private static async Task<IResult> PurgeAsync(
        PurgeLifecycleDataRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(x => x.Id == tenant.OrganizationId, cancellationToken);

        var selectedCategories = request.Categories?
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray() ?? [];
        if (selectedCategories.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["categories"] = ["Select at least one lifecycle category to purge."]
            });
        }

        var supportedKeys = SupportedCategories.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var unsupportedCategories = selectedCategories
            .Where(category => !supportedKeys.Contains(category))
            .ToArray();
        if (unsupportedCategories.Length > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["categories"] = [$"Unsupported lifecycle categories: {string.Join(", ", unsupportedCategories)}."]
            });
        }

        if (!string.Equals(request.Confirmation?.Trim(), organization.Slug, StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["confirmation"] = [$"Confirmation must match the tenant slug '{organization.Slug}'."]
            });
        }

        if (request.CutoffUtc >= DateTimeOffset.UtcNow)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cutoffUtc"] = ["The cutoff must be in the past."]
            });
        }

        var results = new List<PurgeLifecycleCategoryResultResponse>(selectedCategories.Length);
        foreach (var category in selectedCategories)
        {
            var deletedCount = category switch
            {
                "tracking-history" => await PurgeTrackingHistoryAsync(dbContext, tenant.OrganizationId, request.CutoffUtc, cancellationToken),
                "integration-history" => await PurgeIntegrationHistoryAsync(dbContext, tenant.OrganizationId, request.CutoffUtc, cancellationToken),
                "upload-sessions" => await PurgeUploadSessionsAsync(dbContext, tenant.OrganizationId, request.CutoffUtc, cancellationToken),
                _ => 0
            };

            results.Add(new PurgeLifecycleCategoryResultResponse(category, deletedCount));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "admin.data_lifecycle.purged",
            "organization",
            tenant.OrganizationId.ToString(),
            new
            {
                organization = organization.Slug,
                request.CutoffUtc,
                results = results.ToDictionary(x => x.Key, x => x.DeletedCount)
            },
            cancellationToken);

        return Results.Ok(new PurgeLifecycleDataResponse(
            request.CutoffUtc,
            results.Sum(x => x.DeletedCount),
            results));
    }

    private static async Task<int> PurgeTrackingHistoryAsync(
        FleetOpsDbContext dbContext,
        Guid organizationId,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var telemetry = await dbContext.TelemetryPoints
            .Where(x => x.OrganizationId == organizationId && x.RecordedAtUtc < cutoffUtc)
            .ToListAsync(cancellationToken);
        var currentPositions = await dbContext.CurrentVehiclePositions
            .Where(x => x.OrganizationId == organizationId && x.RecordedAtUtc < cutoffUtc)
            .ToListAsync(cancellationToken);

        dbContext.TelemetryPoints.RemoveRange(telemetry);
        dbContext.CurrentVehiclePositions.RemoveRange(currentPositions);
        return telemetry.Count + currentPositions.Count;
    }

    private static async Task<int> PurgeIntegrationHistoryAsync(
        FleetOpsDbContext dbContext,
        Guid organizationId,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var attempts = await dbContext.WebhookDeliveryAttempts
            .Where(x => x.OrganizationId == organizationId && x.AttemptedAtUtc < cutoffUtc)
            .ToListAsync(cancellationToken);
        var receipts = await dbContext.SandboxWebhookReceipts
            .Where(x => x.OrganizationId == organizationId && x.ReceivedAtUtc < cutoffUtc)
            .ToListAsync(cancellationToken);
        var outbox = await dbContext.IntegrationOutboxMessages
            .Where(x => x.OrganizationId == organizationId
                && x.OccurredAtUtc < cutoffUtc
                && (x.Status == IntegrationOutboxStatus.Delivered || x.Status == IntegrationOutboxStatus.DeadLetter))
            .ToListAsync(cancellationToken);

        dbContext.WebhookDeliveryAttempts.RemoveRange(attempts);
        dbContext.SandboxWebhookReceipts.RemoveRange(receipts);
        dbContext.IntegrationOutboxMessages.RemoveRange(outbox);
        return attempts.Count + receipts.Count + outbox.Count;
    }

    private static async Task<int> PurgeUploadSessionsAsync(
        FleetOpsDbContext dbContext,
        Guid organizationId,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.MediaUploadSessions
            .Where(x => x.OrganizationId == organizationId && x.ExpiresAtUtc < cutoffUtc)
            .ToListAsync(cancellationToken);

        dbContext.MediaUploadSessions.RemoveRange(sessions);
        return sessions.Count;
    }
}
