using Microsoft.Extensions.Options;
using SoundSticker.Contracts;
using SoundSticker.Options;
using SoundSticker.Persistence;

namespace SoundSticker.Endpoints;

public static class MediaEndpoints
{
    public static RouteGroupBuilder MapMediaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/media", ListMedia)
            .WithName("ListMedia");

        api.MapGet("/media/{id:guid}", GetMedia)
            .WithName("GetMedia");

        api.MapGet("/media/{id:guid}/file", DownloadMediaFile)
            .WithName("GetMediaFileRaw");

        return api;
    }

    private static IResult ListMedia(IMediaRepository repository, ILogger<Program> logger)
    {
        logger.LogInformation("Media list requested.");
        var media = repository.GetMediaFiles().Select(MediaFileResponse.FromDomain).ToArray();
        logger.LogInformation("Media list returned. Count: {MediaCount}.", media.Length);
        return Results.Ok(media);
    }

    private static IResult GetMedia(Guid id, IMediaRepository repository, ILogger<Program> logger)
    {
        logger.LogInformation("Media requested. MediaFileId: {MediaFileId}.", id);
        var mediaFile = repository.GetMediaFile(id);
        if (mediaFile is null)
        {
            logger.LogWarning("Media not found. MediaFileId: {MediaFileId}.", id);
            return Results.NotFound();
        }

        logger.LogInformation("Media returned. MediaFileId: {MediaFileId}. RelativePath: {RelativePath}.", id, mediaFile.RelativePath);
        return Results.Ok(MediaFileResponse.FromDomain(mediaFile));
    }

    private static IResult DownloadMediaFile(
        Guid id,
        IMediaRepository repository,
        IOptions<StorageOptions> storageOptions,
        ILogger<Program> logger)
    {
        logger.LogInformation("Media file download requested. MediaFileId: {MediaFileId}.", id);
        var mediaFile = repository.GetMediaFile(id);
        if (mediaFile is null)
        {
            logger.LogWarning("Media file download skipped because media was not found. MediaFileId: {MediaFileId}.", id);
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
