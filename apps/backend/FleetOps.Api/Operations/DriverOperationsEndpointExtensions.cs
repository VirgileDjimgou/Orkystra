using FleetOps.Api.Auditing;
using FleetOps.Api.Media;
using FleetOps.Api.Operations;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Dispatch;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.Operations;

public static class DriverOperationsEndpointExtensions
{
    private const string DeliveryPhotoCaption = "Delivery photo";
    private const string RecipientSignatureCaption = "Recipient signature";
    private const string InspectionScope = "inspection";
    private const string DeliveryProofScope = "delivery-proof";

    public static IEndpointRouteBuilder MapDriverOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/driver")
            .RequireAuthorization(AuthorizationPolicies.DriverOnly);

        group.MapGet("/missions/{missionId:guid}/inspection", GetInspectionWorkflowAsync);
        group.MapPost("/missions/{missionId:guid}/inspection", SubmitInspectionAsync);
        group.MapGet("/missions/{missionId:guid}/stops/{stopId:guid}/proof", GetDeliveryProofAsync);
        group.MapPost("/missions/{missionId:guid}/stops/{stopId:guid}/proof", SubmitDeliveryProofAsync);
        group.MapPost("/uploads/sessions", CreateUploadSessionAsync);
        group.MapPost("/uploads/sessions/{sessionId:guid}/chunks", AppendUploadChunkAsync);
        group.MapPost("/uploads/sessions/{sessionId:guid}/complete", CompleteUploadSessionAsync);

        return app;
    }

    private static async Task<IResult> GetInspectionWorkflowAsync(
        Guid missionId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IMediaUrlSigner signer,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var mission = await dbContext.Missions
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == missionId && x.DriverId == driverId, cancellationToken);
        if (mission is null)
        {
            return Results.NotFound();
        }

        var template = await dbContext.ChecklistTemplates
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.IsActive && x.Code == "vehicle-ready",
                cancellationToken);
        if (template is null)
        {
            return Results.Problem("No active inspection checklist is configured for this organization.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var latestInspection = await LoadLatestInspectionAsync(missionId, tenant.OrganizationId, dbContext, signer, cancellationToken);
        return Results.Ok(new DriverInspectionWorkflowResponse(
            mission.Id,
            mission.Reference,
            template.Items
                .OrderBy(x => x.Sequence)
                .Select(x => new ChecklistTemplateItemResponse(x.Sequence, x.Code, x.Label))
                .ToArray(),
            latestInspection));
    }

    private static async Task<IResult> SubmitInspectionAsync(
        Guid missionId,
        SubmitPreDepartureInspectionRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IMediaUrlSigner signer,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var existingReceipt = await dbContext.DriverWorkflowCommandReceipts
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.CommandId == request.CommandId, cancellationToken);
        if (existingReceipt is not null)
        {
            var duplicateInspection = await LoadLatestInspectionAsync(missionId, tenant.OrganizationId, dbContext, signer, cancellationToken);
            return duplicateInspection is null ? Results.NotFound() : Results.Ok(duplicateInspection);
        }

        var mission = await dbContext.Missions
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == missionId && x.DriverId == driverId, cancellationToken);
        if (mission is null)
        {
            return Results.NotFound();
        }

        var template = await dbContext.ChecklistTemplates
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.IsActive && x.Code == "vehicle-ready",
                cancellationToken);
        if (template is null)
        {
            return Results.Problem("No active inspection checklist is configured for this organization.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var validationProblem = await ValidateMediaAssetsAsync(tenant.OrganizationId, request.Items.Select(x => x.PhotoAssetId), dbContext, cancellationToken);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        PreDepartureInspection inspection;
        try
        {
            inspection = new PreDepartureInspection(
                tenant.OrganizationId,
                mission.Id,
                driverId,
                template.Id,
                request.CompletedAtUtc,
                request.Notes,
                request.Items
                    .OrderBy(x => x.Sequence)
                    .Select(x => new InspectionItemResult(
                        tenant.OrganizationId,
                        Guid.NewGuid(),
                        x.Sequence,
                        x.Code,
                        x.Label,
                        x.IsPass,
                        x.DefectSeverity,
                        x.Notes,
                        x.PhotoAssetId))
                    .ToList());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["inspection"] = [ex.Message]
            });
        }

        // Rebind child foreign keys to the persisted inspection identifier.
        var itemResults = inspection.Items
            .OrderBy(x => x.Sequence)
            .Select(x => new InspectionItemResult(
                tenant.OrganizationId,
                inspection.Id,
                x.Sequence,
                x.Code,
                x.Label,
                x.IsPass,
                x.DefectSeverity,
                x.Notes,
                x.PhotoAssetId))
            .ToList();
        inspection = new PreDepartureInspection(
            tenant.OrganizationId,
            mission.Id,
            driverId,
            template.Id,
            request.CompletedAtUtc,
            request.Notes,
            itemResults);

        dbContext.PreDepartureInspections.Add(inspection);
        dbContext.InspectionItemResults.AddRange(inspection.Items);
        dbContext.DriverWorkflowCommandReceipts.Add(new DriverWorkflowCommandReceipt(
            tenant.OrganizationId,
            driverId,
            request.CommandId,
            InspectionScope,
            mission.Id.ToString(),
            request.CompletedAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "driver.inspection_submitted",
            "inspection",
            inspection.Id.ToString(),
            new { mission.Reference, inspection.Outcome, inspection.HasBlockingCriticalDefect },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "driver.inspection_submitted", cancellationToken);

        var response = await LoadLatestInspectionAsync(missionId, tenant.OrganizationId, dbContext, signer, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetDeliveryProofAsync(
        Guid missionId,
        Guid stopId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IMediaUrlSigner signer,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var missionExists = await dbContext.Missions.AnyAsync(
            x => x.OrganizationId == tenant.OrganizationId && x.Id == missionId && x.DriverId == driverId,
            cancellationToken);
        if (!missionExists)
        {
            return Results.NotFound();
        }

        var proof = await LoadDeliveryProofAsync(missionId, stopId, tenant.OrganizationId, dbContext, signer, cancellationToken);
        return proof is null ? Results.Ok(null) : Results.Ok(proof);
    }

    private static async Task<IResult> SubmitDeliveryProofAsync(
        Guid missionId,
        Guid stopId,
        SubmitDeliveryProofRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IAuditService auditService,
        IMediaUrlSigner signer,
        IOperationsRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var existingReceipt = await dbContext.DriverWorkflowCommandReceipts
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.CommandId == request.CommandId, cancellationToken);
        if (existingReceipt is not null)
        {
            var duplicateProof = await LoadDeliveryProofAsync(missionId, stopId, tenant.OrganizationId, dbContext, signer, cancellationToken);
            return duplicateProof is null ? Results.NotFound() : Results.Ok(duplicateProof);
        }

        var mission = await dbContext.Missions
            .Include(x => x.Stops)
            .FirstOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id == missionId && x.DriverId == driverId, cancellationToken);
        if (mission is null)
        {
            return Results.NotFound();
        }

        var stop = mission.Stops.FirstOrDefault(x => x.Id == stopId);
        if (stop is null)
        {
            return Results.NotFound();
        }

        if (!request.Photos.Any(x => string.Equals(x.Caption, DeliveryPhotoCaption, StringComparison.Ordinal))
            || !request.Photos.Any(x => string.Equals(x.Caption, RecipientSignatureCaption, StringComparison.Ordinal)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["photos"] = ["A delivery photo and a handwritten recipient signature are required."]
            });
        }

        var validationProblem = await ValidateMediaAssetsAsync(
            tenant.OrganizationId,
            request.Photos.Select(x => (Guid?)x.MediaAssetId),
            dbContext,
            cancellationToken);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var existingProof = await dbContext.DeliveryProofs
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.MissionId == missionId && x.MissionStopId == stopId,
                cancellationToken);
        if (existingProof is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["proof"] = ["Delivery proof already exists for this mission stop."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        DeliveryProof proof;
        try
        {
            var proofId = Guid.NewGuid();
            var proofPhotos = request.Photos.Select(x => new DeliveryProofPhoto(
                tenant.OrganizationId,
                proofId,
                x.MediaAssetId,
                x.Caption)).ToList();
            proof = new DeliveryProof(
                tenant.OrganizationId,
                mission.Id,
                stop.Id,
                driverId,
                request.RecipientName,
                request.SignatureName,
                request.DeliveredAtUtc,
                request.Notes,
                proofPhotos,
                proofId);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["proof"] = [ex.Message]
            });
        }

        dbContext.DeliveryProofs.Add(proof);
        dbContext.DeliveryProofPhotos.AddRange(proof.Photos);
        dbContext.DriverWorkflowCommandReceipts.Add(new DriverWorkflowCommandReceipt(
            tenant.OrganizationId,
            driverId,
            request.CommandId,
            DeliveryProofScope,
            stop.Id.ToString(),
            request.DeliveredAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            tenant.OrganizationId,
            tenant.UserId,
            "driver.delivery_proof_submitted",
            "delivery-proof",
            proof.Id.ToString(),
            new { mission.Reference, stop.Name, request.RecipientName },
            cancellationToken);
        await notifier.NotifyQueueChangedAsync(tenant.OrganizationId, "driver.delivery_proof_submitted", cancellationToken);

        var response = await LoadDeliveryProofAsync(missionId, stopId, tenant.OrganizationId, dbContext, signer, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateUploadSessionAsync(
        UploadSessionRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IOptions<MediaUploadSecurityOptions> uploadSecurityOptions,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var maximumBytes = Math.Max(1, uploadSecurityOptions.Value.MaximumBytes);
        if (request.TotalBytes > maximumBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["totalBytes"] = [$"Driver media uploads cannot exceed {maximumBytes} bytes."]
            });
        }

        if (!string.Equals(request.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["contentType"] = ["Only JPEG and PNG driver media is accepted."]
            });
        }

        MediaUploadSession session;
        try
        {
            session = new MediaUploadSession(
                tenant.OrganizationId,
                driverId,
                request.Purpose,
                request.FileName,
                request.ContentType,
                request.TotalBytes,
                DateTimeOffset.UtcNow.AddHours(1),
                $"{tenant.OrganizationId:D}/temp/{Guid.NewGuid():D}.bin");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["upload"] = [ex.Message]
            });
        }

        dbContext.MediaUploadSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToUploadSessionResponse(session));
    }

    private static async Task<IResult> AppendUploadChunkAsync(
        Guid sessionId,
        AppendUploadChunkRequest request,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IPrivateMediaStorage storage,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var session = await dbContext.MediaUploadSessions
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.Id == sessionId && x.DriverId == driverId,
                cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        if (session.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["session"] = ["Upload session expired. Create a new upload session and retry."]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.Base64Content);
        }
        catch (FormatException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["base64Content"] = ["Upload chunks must contain valid Base64 content."]
            });
        }
        try
        {
            await storage.AppendAsync(session.TempStorageKey, request.Offset, bytes, cancellationToken);
            session.Advance(bytes.LongLength);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (FormatException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["base64Content"] = ["Chunk payload must be valid base64."]
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["offset"] = [ex.Message]
            }, statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(ToUploadSessionResponse(session));
    }

    private static async Task<IResult> CompleteUploadSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IPrivateMediaStorage storage,
        IUploadedContentScanner contentScanner,
        IMediaUrlSigner signer,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        if (tenant.DriverId is not Guid driverId)
        {
            return Results.Forbid();
        }

        var session = await dbContext.MediaUploadSessions
            .FirstOrDefaultAsync(
                x => x.OrganizationId == tenant.OrganizationId && x.Id == sessionId && x.DriverId == driverId,
                cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        if (session.IsCompleted && session.MediaAssetId is Guid completedAssetId)
        {
            var existingAsset = await dbContext.MediaAssets.FirstAsync(x => x.Id == completedAssetId, cancellationToken);
            return Results.Ok(new MediaAssetResponse(
                existingAsset.Id,
                existingAsset.FileName,
                existingAsset.ContentType,
                existingAsset.SizeBytes,
                signer.CreateReadUrl(existingAsset.Id, TimeSpan.FromMinutes(30))));
        }

        if (session.ScanDisposition == UploadedContentDisposition.Quarantine)
        {
            return Results.Problem(
                title: "Upload quarantined",
                detail: "This upload failed content validation and cannot be published.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        try
        {
            var (content, _, _) = await storage.OpenReadAsync(
                session.TempStorageKey,
                session.ContentType,
                session.FileName,
                cancellationToken);
            UploadedContentScanResult scanResult;
            await using (content)
            {
                scanResult = await contentScanner.ScanAsync(content, session.ContentType, cancellationToken);
                session.RecordScan(scanResult);
            }

            if (scanResult.Disposition == UploadedContentDisposition.Quarantine)
            {
                var quarantineKey = $"{tenant.OrganizationId:D}/quarantine/{session.Id:D}.bin";
                await storage.MoveAsync(session.TempStorageKey, quarantineKey, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await auditService.WriteAsync(
                    tenant.OrganizationId,
                    tenant.UserId,
                    "media.upload_quarantined",
                    "upload_session",
                    session.Id.ToString(),
                    new { session.Purpose, session.ContentType, session.TotalBytes, scanResult.Reason },
                    cancellationToken);
                return Results.Problem(
                    title: "Upload quarantined",
                    detail: "The upload did not pass content validation and was isolated before publication.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var asset = new MediaAsset(
                tenant.OrganizationId,
                $"{tenant.OrganizationId:D}/media/{Guid.NewGuid():D}-{session.FileName}",
                session.FileName,
                session.ContentType,
                session.TotalBytes);
            await storage.MoveAsync(session.TempStorageKey, asset.StorageKey, cancellationToken);
            session.Complete(asset.Id);
            dbContext.MediaAssets.Add(asset);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new MediaAssetResponse(
                asset.Id,
                asset.FileName,
                asset.ContentType,
                asset.SizeBytes,
                signer.CreateReadUrl(asset.Id, TimeSpan.FromMinutes(30))));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["upload"] = [ex.Message]
            }, statusCode: StatusCodes.Status409Conflict);
        }
    }

    internal static async Task<PreDepartureInspectionResponse?> LoadLatestInspectionAsync(
        Guid missionId,
        Guid organizationId,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        CancellationToken cancellationToken)
    {
        var inspection = await dbContext.PreDepartureInspections
            .Include(x => x.Items)
            .Where(x => x.OrganizationId == organizationId && x.MissionId == missionId)
            .OrderByDescending(x => x.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (inspection is null)
        {
            return null;
        }

        var assetIds = inspection.Items
            .Where(x => x.PhotoAssetId.HasValue)
            .Select(x => x.PhotoAssetId!.Value)
            .Distinct()
            .ToList();
        var assets = await dbContext.MediaAssets
            .Where(x => x.OrganizationId == organizationId && assetIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return new PreDepartureInspectionResponse(
            inspection.Id,
            inspection.Outcome,
            inspection.HasBlockingCriticalDefect,
            inspection.CompletedAtUtc,
            inspection.Notes,
            inspection.Items
                .OrderBy(x => x.Sequence)
                .Select(x => new InspectionItemResultResponse(
                    x.Sequence,
                    x.Code,
                    x.Label,
                    x.IsPass,
                    x.DefectSeverity,
                    x.Notes,
                    x.PhotoAssetId is Guid photoAssetId && assets.TryGetValue(photoAssetId, out var asset)
                        ? new MediaAssetResponse(asset.Id, asset.FileName, asset.ContentType, asset.SizeBytes, signer.CreateReadUrl(asset.Id, TimeSpan.FromMinutes(30)))
                        : null))
                .ToList());
    }

    internal static async Task<DeliveryProofResponse?> LoadDeliveryProofAsync(
        Guid missionId,
        Guid stopId,
        Guid organizationId,
        FleetOpsDbContext dbContext,
        IMediaUrlSigner signer,
        CancellationToken cancellationToken)
    {
        var proof = await dbContext.DeliveryProofs
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.MissionId == missionId && x.MissionStopId == stopId,
                cancellationToken);
        if (proof is null)
        {
            return null;
        }

        var assetIds = proof.Photos.Select(x => x.MediaAssetId).Distinct().ToList();
        var assets = await dbContext.MediaAssets
            .Where(x => x.OrganizationId == organizationId && assetIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return new DeliveryProofResponse(
            proof.Id,
            proof.MissionStopId,
            proof.RecipientName,
            proof.SignatureName,
            proof.DeliveredAtUtc,
            proof.Notes,
            proof.Photos
                .Select(x => new DeliveryProofPhotoResponse(
                    x.MediaAssetId,
                    x.Caption,
                    new MediaAssetResponse(
                        assets[x.MediaAssetId].Id,
                        assets[x.MediaAssetId].FileName,
                        assets[x.MediaAssetId].ContentType,
                        assets[x.MediaAssetId].SizeBytes,
                        signer.CreateReadUrl(assets[x.MediaAssetId].Id, TimeSpan.FromMinutes(30)))))
                .ToList());
    }

    private static UploadSessionResponse ToUploadSessionResponse(MediaUploadSession session) =>
        new(
            session.Id,
            session.UploadedBytes,
            session.TotalBytes,
            session.ExpiresAtUtc,
            session.IsCompleted,
            session.MediaAssetId);

    private static async Task<IResult?> ValidateMediaAssetsAsync(
        Guid organizationId,
        IEnumerable<Guid?> assetIds,
        FleetOpsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var requiredAssetIds = assetIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (requiredAssetIds.Count == 0)
        {
            return null;
        }

        var existingCount = await dbContext.MediaAssets.CountAsync(
            x => x.OrganizationId == organizationId && requiredAssetIds.Contains(x.Id),
            cancellationToken);
        if (existingCount != requiredAssetIds.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["media"] = ["One or more media assets do not exist in this organization."]
            });
        }

        return null;
    }
}
