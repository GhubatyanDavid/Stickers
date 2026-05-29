using Microsoft.Extensions.Options;
using Npgsql;
using SoundSticker.Contracts;
using SoundSticker.Domain;
using SoundSticker.FileStorage;
using SoundSticker.Infrastructure;
using SoundSticker.Options;
using SoundSticker.Persistence;
using SoundSticker.Processing;

namespace SoundSticker.Endpoints;

public static class UploadEndpoints
{
    public static RouteGroupBuilder MapUploadEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/uploads", UploadMediaAsync)
            .DisableAntiforgery()
            .RequireRateLimiting("uploads")
            .WithName("UploadMedia");

        return api;
    }

    private static async Task<IResult> UploadMediaAsync(
        IFormFile file,
        ILocalFileStorage storage,
        IStoredFileManager storedFileManager,
        IMediaRepository repository,
        IMediaPreviewAnalyzer previewAnalyzer,
        IOptions<StorageOptions> options,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new ProblemResponse("Upload file is empty."));
        }

        if (file.Length > options.Value.MaxUploadBytes)
        {
            return Results.BadRequest(new ProblemResponse($"Upload is too large. Max size is {options.Value.MaxUploadBytes} bytes."));
        }

        var contentType = file.ContentType ?? "image/jpeg";
        var fileName = file.FileName ?? "file.jpg";
        var mediaKind = MediaKindDetector.From(fileName, contentType);

        logger.LogInformation(
            "Upload received. FileName: {FileName}. ContentType: {ContentType}. SizeBytes: {SizeBytes}. DetectedKind: {MediaKind}.",
            fileName,
            contentType,
            file.Length,
            mediaKind);

        if (mediaKind == MediaKind.Unknown)
        {
            if (contentType.StartsWith("image/") || fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg") || fileName.EndsWith(".png"))
            {
                mediaKind = MediaKind.Image;
            }
            else
            {
                return Results.BadRequest(new ProblemResponse("Unsupported media type. Upload video, audio, image, or GIF."));
            }
        }

        var savedFile = await storage.SaveOriginalAsync(file, mediaKind, cancellationToken);
        logger.LogInformation(
            "Upload file saved. MediaFileId: {MediaFileId}. RelativePath: {RelativePath}.",
            savedFile.Id,
            savedFile.RelativePath);

        var mediaFile = MediaFile.Create(
            savedFile.Id,
            fileName,
            mediaKind,
            contentType,
            file.Length,
            savedFile.RelativePath,
            savedFile.PublicUrl);

        try
        {
            repository.AddMediaFile(mediaFile);
            logger.LogInformation("Media metadata saved. MediaFileId: {MediaFileId}.", mediaFile.Id);
        }
        catch (NpgsqlException exception)
        {
            logger.LogError(
                "Could not save media metadata. MediaFileId: {MediaFileId}. SqlState: {SqlState}. Message: {MessageText}",
                mediaFile.Id,
                PostgresDataSourceFactory.TryGetSqlState(exception),
                exception.Message);
            storedFileManager.DeleteStoredFile(savedFile.RelativePath, logger);
            return Results.Problem(
                title: "Database is unavailable.",
                detail: "Could not save uploaded media metadata. Check the PostgreSQL connection settings.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var preview = await previewAnalyzer.AnalyzeAsync(mediaFile, cancellationToken);
        if (preview is null)
        {
            logger.LogInformation("No preview metadata generated for media {MediaFileId}.", mediaFile.Id);
            return Results.Created($"/api/media/{mediaFile.Id}", MediaFileResponse.FromDomain(mediaFile));
        }

        mediaFile.SetPreview(preview);
        try
        {
            repository.UpdateMediaFile(mediaFile);
            logger.LogInformation(
                "Media preview metadata saved. MediaFileId: {MediaFileId}. DurationMs: {DurationMs}. Width: {Width}. Height: {Height}. HasAudio: {HasAudio}.",
                mediaFile.Id,
                preview.DurationMs,
                preview.Width,
                preview.Height,
                preview.HasAudio);
        }
        catch (NpgsqlException exception)
        {
            logger.LogWarning(
                "Could not save media preview metadata. MediaFileId: {MediaFileId}. SqlState: {SqlState}. Message: {MessageText}",
                mediaFile.Id,
                PostgresDataSourceFactory.TryGetSqlState(exception),
                exception.Message);
        }

        return Results.Created($"/api/media/{mediaFile.Id}", MediaFileResponse.FromDomain(mediaFile));
    }
}
