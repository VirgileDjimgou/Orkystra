using FleetOps.Core.Modules.Operations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Api.Media;

public static class MediaEndpointExtensions
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/media/{assetId:guid}", GetMediaAsync).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> GetMediaAsync(
        Guid assetId,
        long expires,
        string signature,
        FleetOpsDbContext dbContext,
        IPrivateMediaStorage storage,
        IMediaUrlSigner signer,
        CancellationToken cancellationToken)
    {
        if (!signer.IsValid(assetId, expires, signature))
        {
            return Results.Unauthorized();
        }

        var asset = await dbContext.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == assetId, cancellationToken);
        if (asset is null)
        {
            return Results.NotFound();
        }

        var (stream, contentType, fileName) = await storage.OpenReadAsync(
            asset.StorageKey,
            asset.ContentType,
            asset.FileName,
            cancellationToken);
        return Results.File(stream, contentType, fileDownloadName: fileName);
    }
}
