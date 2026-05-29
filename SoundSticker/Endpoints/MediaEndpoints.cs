using Microsoft.Extensions.Options;
using SoundSticker.Auth;
using SoundSticker.Contracts;
using SoundSticker.Options;
using SoundSticker.Persistence;

namespace SoundSticker.Endpoints;

public static class MediaEndpoints
{
    public static RouteGroupBuilder MapMediaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/media", ListMedia)
            .WithName("ListMedia")
            .WithSummary("List my media")
            .WithDescription("Returns uploaded media owned by the current X-User-Id.")
            .Produces<MediaFileResponse[]>()
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

        api.MapGet("/media/{id:guid}", GetMedia)
            .WithName("GetMedia")
            .WithSummary("Get my media metadata")
            .WithDescription("Returns one uploaded media item if it belongs to the current X-User-Id.")
            .Produces<MediaFileResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

        api.MapGet("/media/{id:guid}/file", DownloadMediaFile)
            .WithName("GetMediaFileRaw")
            .WithSummary("Download my original media file")
            .WithDescription("Streams the original uploaded file if it belongs to the current X-User-Id.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

        return api;
    }

    private static IResult ListMedia(IMediaRepository repository, ICurrentUser currentUser, ILogger<Program> logger)
    {
        var ownerUserId = currentUser.UserId;
        logger.LogInformation("Media list requested. OwnerUserId: {OwnerUserId}.", ownerUserId);
        var media = repository.GetMediaFilesByOwner(ownerUserId).Select(MediaFileResponse.FromDomain).ToArray();
        logger.LogInformation("Media list returned. OwnerUserId: {OwnerUserId}. Count: {MediaCount}.", ownerUserId, media.Length);
        return Results.Ok(media);
    }

    private static IResult GetMedia(Guid id, IMediaRepository repository, ICurrentUser currentUser, ILogger<Program> logger)
    {
        logger.LogInformation("Media requested. MediaFileId: {MediaFileId}.", id);
        var mediaFile = repository.GetMediaFile(id);
        if (mediaFile is null)
        {
            logger.LogWarning("Media not found. MediaFileId: {MediaFileId}.", id);
            return Results.NotFound();
        }

        if (mediaFile.OwnerUserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Media request forbidden. MediaFileId: {MediaFileId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                mediaFile.OwnerUserId,
                currentUser.UserId);
            return Results.NotFound();
        }

        logger.LogInformation("Media returned. MediaFileId: {MediaFileId}. RelativePath: {RelativePath}.", id, mediaFile.RelativePath);
        return Results.Ok(MediaFileResponse.FromDomain(mediaFile));
    }

    private static IResult DownloadMediaFile(
        Guid id,
        IMediaRepository repository,
        IOptions<StorageOptions> storageOptions,
        ICurrentUser currentUser,
        ILogger<Program> logger)
    {
        logger.LogInformation("Media file download requested. MediaFileId: {MediaFileId}.", id);
        var mediaFile = repository.GetMediaFile(id);
        if (mediaFile is null)
        {
            logger.LogWarning("Media file download skipped because media was not found. MediaFileId: {MediaFileId}.", id);
            return Results.NotFound();
        }

        if (mediaFile.OwnerUserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Media file download forbidden. MediaFileId: {MediaFileId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                mediaFile.OwnerUserId,
                currentUser.UserId);
            return Results.NotFound();
        }

        var rootPath = storageOptions.Value.GetResolvedRootPath(Directory.GetCurrentDirectory());
        var fullPath = Path.Combine(rootPath, mediaFile.RelativePath);

        if (!File.Exists(fullPath))
        {
            logger.LogWarning("Media file download skipped because file was not found. MediaFileId: {MediaFileId}. FullPath: {FullPath}.", id, fullPath);
            return Results.NotFound();
        }

        logger.LogInformation("Media file download started. MediaFileId: {MediaFileId}. FullPath: {FullPath}.", id, fullPath);
        return Results.File(fullPath, mediaFile.ContentType);
    }
}
