using FleetOps.Api.Auditing;
using FleetOps.Api.Security;
using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Media;

public static class MediaEndpointExtensions
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/media/{assetId:guid}", GetMediaAsync).RequireAuthorization();
        app.MapDelete("/api/v1/media/{assetId:guid}", RevokeMediaAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        return app;
    }

    private static async Task<IResult> GetMediaAsync(
        Guid assetId,
        long expires,
        string signature,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IPrivateMediaStorage storage,
        IMediaUrlSigner signer,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);

        var asset = await dbContext.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == assetId && x.OrganizationId == tenant.OrganizationId, cancellationToken);
        if (asset is null)
        {
            return Results.NotFound();
        }

        if (asset.IsReadRevoked || asset.RetainUntilUtc <= DateTimeOffset.UtcNow
            || !signer.IsValid(assetId, tenant.OrganizationId, expires, signature))
        {
            return Results.Unauthorized();
        }

        try
        {
            var (stream, contentType, fileName) = await storage.OpenReadAsync(
                asset.StorageKey,
                asset.ContentType,
                asset.FileName,
                cancellationToken);
            return Results.File(stream, contentType, fileDownloadName: fileName);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            return Results.Problem(title: "Media storage unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> RevokeMediaAsync(
        Guid assetId,
        HttpContext httpContext,
        FleetOpsDbContext dbContext,
        ICurrentTenantAccessor currentTenantAccessor,
        IPrivateMediaStorage storage,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var tenant = currentTenantAccessor.GetRequiredTenant(httpContext.User);
        var asset = await dbContext.MediaAssets.FirstOrDefaultAsync(
            x => x.Id == assetId && x.OrganizationId == tenant.OrganizationId,
            cancellationToken);
        if (asset is null) return Results.NotFound();
        if (!asset.IsReadRevoked)
        {
            await storage.DeleteAsync(asset.StorageKey, cancellationToken);
            asset.RevokeReadAccess(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync(
                tenant.OrganizationId,
                tenant.UserId,
                "media.read_revoked",
                "media_asset",
                asset.Id.ToString(),
                new { asset.ContentType, asset.SizeBytes },
                cancellationToken);
        }
        return Results.NoContent();
    }
}
