using Microsoft.Extensions.Options;
using SoundSticker.Auth;
using SoundSticker.Contracts;
using SoundSticker.Domain;
using SoundSticker.FileStorage;
using SoundSticker.Options;
using SoundSticker.Persistence;
using SoundSticker.Processing;

namespace SoundSticker.Endpoints;

public static class StickerEndpoints
{
    public static RouteGroupBuilder MapStickerEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/stickers/from-video", CreateVideoStickerAsync)
            .RequireRateLimiting("sticker-creation")
            .WithName("CreateVideoSticker");

        api.MapPost("/stickers/from-image", CreateImageStickerAsync)
            .RequireRateLimiting("sticker-creation")
            .WithName("CreateImageSticker");

        api.MapGet("/stickers/my", ListMyStickers)
            .WithName("ListMyStickers");

        api.MapGet("/stickers", ListMyStickers)
            .WithName("ListStickers");

        api.MapGet("/stickers/all", ListAllStickers)
            .WithName("ListAllStickers");

        api.MapGet("/stickers/{id:guid}", GetSticker)
            .WithName("GetSticker");

        api.MapGet("/stickers/{id:guid}/status", GetStickerStatus)
            .WithName("GetStickerStatus");

        api.MapGet("/stickers/{id:guid}/download", DownloadSticker)
            .WithName("DownloadSticker");

        api.MapDelete("/stickers/{id:guid}", DeleteSticker)
            .WithName("DeleteSticker");

        return api;
    }

    private static async Task<IResult> CreateVideoStickerAsync(
        CreateVideoStickerRequest request,
        IMediaRepository repository,
        StickerProcessingQueue queue,
        IOptions<StickerOptions> stickerOptions,
        ICurrentUser currentUser,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
        await CreateStickerAsync(
            request,
            repository,
            queue,
            stickerOptions,
            currentUser,
            logger,
            requiredSourceKind: null,
            cancellationToken);

    private static async Task<IResult> CreateImageStickerAsync(
        CreateVideoStickerRequest request,
        IMediaRepository repository,
        StickerProcessingQueue queue,
        IOptions<StickerOptions> stickerOptions,
        ICurrentUser currentUser,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
        await CreateStickerAsync(
            request,
            repository,
            queue,
            stickerOptions,
            currentUser,
            logger,
            MediaKind.Image,
            cancellationToken);

    private static IResult ListMyStickers(IMediaRepository repository, ICurrentUser currentUser, ILogger<Program> logger)
    {
        var ownerUserId = currentUser.UserId;
        logger.LogInformation("My sticker list requested. OwnerUserId: {OwnerUserId}.", ownerUserId);
        var stickers = repository.GetStickersByOwner(ownerUserId).Select(StickerResponse.FromDomain).ToArray();
        logger.LogInformation("My sticker list returned. OwnerUserId: {OwnerUserId}. Count: {StickerCount}.", ownerUserId, stickers.Length);
        return Results.Ok(stickers);
    }

    private static IResult ListAllStickers(IMediaRepository repository, ILogger<Program> logger)
    {
        logger.LogInformation("All public sticker list requested.");
        var stickers = repository.GetPublicStickers().Select(StickerResponse.FromDomain).ToArray();
        logger.LogInformation("All public sticker list returned. Count: {StickerCount}.", stickers.Length);
        return Results.Ok(stickers);
    }

    private static IResult GetSticker(Guid id, IMediaRepository repository, ICurrentUser currentUser, ILogger<Program> logger)
    {
        logger.LogInformation("Sticker requested. StickerId: {StickerId}.", id);
        var sticker = repository.GetSticker(id);
        if (sticker is null)
        {
            logger.LogWarning("Sticker not found. StickerId: {StickerId}.", id);
            return Results.NotFound();
        }

        if (sticker.OwnerUserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Sticker request forbidden. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                sticker.OwnerUserId,
                currentUser.UserId);
            return Results.NotFound();
        }

        logger.LogInformation("Sticker returned. StickerId: {StickerId}. Status: {StickerStatus}. OutputUrl: {OutputUrl}.", id, sticker.Status, sticker.OutputUrl);
        return Results.Ok(StickerResponse.FromDomain(sticker));
    }

    private static IResult GetStickerStatus(Guid id, IMediaRepository repository, ICurrentUser currentUser, ILogger<Program> logger)
    {
        logger.LogInformation("Sticker status requested. StickerId: {StickerId}.", id);
        var sticker = repository.GetSticker(id);
        if (sticker is null)
        {
            logger.LogWarning("Sticker status not found. StickerId: {StickerId}.", id);
            return Results.NotFound();
        }

        if (sticker.OwnerUserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Sticker status forbidden. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                sticker.OwnerUserId,
                currentUser.UserId);
            return Results.NotFound();
        }

        logger.LogInformation(
            "Sticker status returned. StickerId: {StickerId}. Status: {StickerStatus}. OutputUrl: {OutputUrl}. ErrorMessage: {ErrorMessage}.",
            id,
            sticker.Status,
            sticker.OutputUrl,
            sticker.ErrorMessage);
        return Results.Ok(new StickerStatusResponse(sticker.Id, sticker.Status, sticker.ErrorMessage, sticker.OutputUrl));
    }

    private static async Task<IResult> CreateStickerAsync(
        CreateVideoStickerRequest request,
        IMediaRepository repository,
        StickerProcessingQueue queue,
        IOptions<StickerOptions> stickerOptions,
        ICurrentUser currentUser,
        ILogger logger,
        MediaKind? requiredSourceKind,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Sticker creation requested. SourceMediaId: {SourceMediaId}. CoverImageId: {CoverImageId}. AudioSourceMediaId: {AudioSourceMediaId}. AudioMode: {AudioMode}. TrimStartMs: {TrimStartMs}. TrimEndMs: {TrimEndMs}. IsPublic: {IsPublic}.",
            request.SourceMediaId,
            request.CoverImageId,
            request.AudioSourceMediaId,
            request.AudioMode,
            request.TrimStartMs,
            request.TrimEndMs,
            request.IsPublic);

        var sourceMedia = repository.GetMediaFile(request.SourceMediaId);
        if (sourceMedia is null)
        {
            return Results.NotFound(new ProblemResponse("Source media was not found."));
        }

        var ownerUserId = currentUser.UserId;
        if (sourceMedia.OwnerUserId != ownerUserId)
        {
            logger.LogWarning(
                "Sticker creation rejected because source media belongs to another user. SourceMediaId: {SourceMediaId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                sourceMedia.Id,
                sourceMedia.OwnerUserId,
                ownerUserId);
            return Results.NotFound(new ProblemResponse("Source media was not found."));
        }

        var validationResult = ValidateSource(request, sourceMedia, requiredSourceKind, stickerOptions.Value);
        if (validationResult is not null)
        {
            return validationResult;
        }

        if (request.CoverImageId.HasValue)
        {
            var coverImage = repository.GetMediaFile(request.CoverImageId.Value);
            if (coverImage is null || coverImage.OwnerUserId != ownerUserId)
            {
                return Results.BadRequest(new ProblemResponse("Cover image media was not found."));
            }
        }

        var audioMode = GetEffectiveAudioMode(request.AudioMode, sourceMedia, logger);
        var audioTrimStartMs = request.AudioTrimStartMs ?? request.TrimStartMs;
        var audioTrimEndMs = request.AudioTrimEndMs ?? request.TrimEndMs;

        var audioValidationResult = ValidateAudioRequest(
            request,
            sourceMedia,
            audioMode,
            audioTrimStartMs,
            audioTrimEndMs,
            repository,
            ownerUserId,
            out var audioSourceMedia,
            logger);
        if (audioValidationResult is not null)
        {
            return audioValidationResult;
        }

        var sticker = Sticker.CreateVideoSticker(
            Guid.NewGuid(),
            sourceMedia.Id,
            request.CoverImageId,
            audioSourceMedia?.Id,
            request.TrimStartMs,
            request.TrimEndMs,
            audioTrimStartMs,
            audioTrimEndMs,
            audioMode,
            ownerUserId,
            request.IsPublic);

        repository.AddSticker(sticker);
        await queue.EnqueueAsync(sticker.Id, cancellationToken);
        logger.LogInformation(
            "Sticker queued. StickerId: {StickerId}. SourceMediaId: {SourceMediaId}. IsPublic: {IsPublic}.",
            sticker.Id,
            sticker.SourceMediaId,
            sticker.IsPublic);

        return Results.Accepted($"/api/stickers/{sticker.Id}", StickerResponse.FromDomain(sticker));
    }

    private static IResult? ValidateSource(
        CreateVideoStickerRequest request,
        MediaFile sourceMedia,
        MediaKind? requiredSourceKind,
        StickerOptions stickerOptions)
    {
        if (requiredSourceKind.HasValue && sourceMedia.Kind != requiredSourceKind.Value)
        {
            return Results.BadRequest(new ProblemResponse($"Source media must be an {requiredSourceKind.Value.ToString().ToLowerInvariant()}."));
        }

        if (sourceMedia.Kind is not (MediaKind.Video or MediaKind.Image))
        {
            return Results.BadRequest(new ProblemResponse("Source media must be a video or image."));
        }

        if (sourceMedia.Kind == MediaKind.Video && !HasUsablePreview(sourceMedia))
        {
            return Results.BadRequest(new ProblemResponse("Source video preview metadata is unavailable. Check FFprobe and upload the file again."));
        }

        if (request.TrimStartMs < 0 || request.TrimEndMs <= request.TrimStartMs)
        {
            return Results.BadRequest(new ProblemResponse("Trim range is invalid."));
        }

        var durationMs = request.TrimEndMs - request.TrimStartMs;
        if (durationMs > stickerOptions.MaxDurationMs)
        {
            return Results.BadRequest(new ProblemResponse($"Sticker can be at most {stickerOptions.MaxDurationMs} ms."));
        }

        if (sourceMedia.Kind == MediaKind.Video && IsOutsideMediaDuration(request.TrimEndMs, sourceMedia))
        {
            return Results.BadRequest(new ProblemResponse("Video trim range exceeds the source video duration."));
        }

        if (!Enum.IsDefined(request.AudioMode))
        {
            return Results.BadRequest(new ProblemResponse("Audio mode is invalid."));
        }

        return null;
    }

    private static StickerAudioMode GetEffectiveAudioMode(
        StickerAudioMode requestedAudioMode,
        MediaFile sourceMedia,
        ILogger logger)
    {
        if (requestedAudioMode != StickerAudioMode.KeepOriginal ||
            (sourceMedia.Kind != MediaKind.Image && sourceMedia.Preview?.HasAudio != false))
        {
            return requestedAudioMode;
        }

        logger.LogWarning(
            "Sticker requested KeepOriginal audio, but source media has no audio. Falling back to Mute. SourceMediaId: {SourceMediaId}.",
            sourceMedia.Id);
        return StickerAudioMode.Mute;
    }

    private static IResult? ValidateAudioRequest(
        CreateVideoStickerRequest request,
        MediaFile sourceMedia,
        StickerAudioMode audioMode,
        int audioTrimStartMs,
        int audioTrimEndMs,
        IMediaRepository repository,
        string ownerUserId,
        out MediaFile? audioSourceMedia,
        ILogger logger)
    {
        audioSourceMedia = null;

        if (audioMode != StickerAudioMode.Mute &&
            (audioTrimStartMs < 0 || audioTrimEndMs <= audioTrimStartMs))
        {
            logger.LogWarning(
                "Sticker creation rejected because audio trim range is invalid. SourceMediaId: {SourceMediaId}. AudioTrimStartMs: {AudioTrimStartMs}. AudioTrimEndMs: {AudioTrimEndMs}.",
                request.SourceMediaId,
                audioTrimStartMs,
                audioTrimEndMs);
            return Results.BadRequest(new ProblemResponse("Audio trim range is invalid."));
        }

        if (audioMode == StickerAudioMode.UseMedia)
        {
            return ValidateExternalAudio(request, audioTrimEndMs, repository, ownerUserId, out audioSourceMedia, logger);
        }

        if (request.AudioSourceMediaId.HasValue)
        {
            logger.LogWarning(
                "Sticker creation rejected because audio source media was provided for audio mode {AudioMode}. SourceMediaId: {SourceMediaId}. AudioSourceMediaId: {AudioSourceMediaId}.",
                audioMode,
                request.SourceMediaId,
                request.AudioSourceMediaId);
            return Results.BadRequest(new ProblemResponse("Audio source media can only be used with UseMedia mode."));
        }

        if (audioMode == StickerAudioMode.KeepOriginal)
        {
            return ValidateOriginalAudio(sourceMedia, audioTrimEndMs, logger);
        }

        return null;
    }

    private static IResult? ValidateExternalAudio(
        CreateVideoStickerRequest request,
        int audioTrimEndMs,
        IMediaRepository repository,
        string ownerUserId,
        out MediaFile? audioSourceMedia,
        ILogger logger)
    {
        audioSourceMedia = null;
        if (!request.AudioSourceMediaId.HasValue)
        {
            logger.LogWarning("Sticker creation rejected because audio source media is required for UseMedia mode. SourceMediaId: {SourceMediaId}.", request.SourceMediaId);
            return Results.BadRequest(new ProblemResponse("Audio source media is required for UseMedia mode."));
        }

        audioSourceMedia = repository.GetMediaFile(request.AudioSourceMediaId.Value);
        if (audioSourceMedia is null || audioSourceMedia.OwnerUserId != ownerUserId)
        {
            return Results.BadRequest(new ProblemResponse("Audio source media was not found."));
        }

        if (audioSourceMedia.Kind is not (MediaKind.Audio or MediaKind.Video))
        {
            return Results.BadRequest(new ProblemResponse("Audio source media must be audio or video."));
        }

        if (!HasUsablePreview(audioSourceMedia))
        {
            return Results.BadRequest(new ProblemResponse("Audio source preview metadata is unavailable. Check FFprobe and upload the file again."));
        }

        if (!audioSourceMedia.Preview!.HasAudio)
        {
            return Results.BadRequest(new ProblemResponse("Audio source media does not contain an audio stream."));
        }

        return IsOutsideMediaDuration(audioTrimEndMs, audioSourceMedia)
            ? Results.BadRequest(new ProblemResponse("Audio trim range exceeds the audio source duration."))
            : null;
    }

    private static IResult? ValidateOriginalAudio(MediaFile sourceMedia, int audioTrimEndMs, ILogger logger)
    {
        if (sourceMedia.Kind == MediaKind.Image)
        {
            logger.LogWarning("Sticker creation rejected because image source cannot keep original audio. SourceMediaId: {SourceMediaId}.", sourceMedia.Id);
            return Results.BadRequest(new ProblemResponse("Image source media does not contain original audio. Use Mute or choose another audio source."));
        }

        if (!sourceMedia.Preview!.HasAudio)
        {
            logger.LogWarning("Sticker creation rejected because source video has no audio. SourceMediaId: {SourceMediaId}.", sourceMedia.Id);
            return Results.BadRequest(new ProblemResponse("Source video does not contain an audio stream. Use Mute or choose another audio source."));
        }

        return IsOutsideMediaDuration(audioTrimEndMs, sourceMedia)
            ? Results.BadRequest(new ProblemResponse("Audio trim range exceeds the source video duration."))
            : null;
    }

    private static IResult DownloadSticker(
        Guid id,
        IMediaRepository repository,
        IStoredFileManager storedFileManager,
        ICurrentUser currentUser,
        ILogger<Program> logger)
    {
        logger.LogInformation("Sticker download requested. StickerId: {StickerId}.", id);

        var sticker = repository.GetSticker(id);
        if (sticker is null)
        {
            logger.LogWarning("Sticker download skipped because sticker was not found. StickerId: {StickerId}.", id);
            return Results.NotFound(new ProblemResponse("Sticker was not found."));
        }

        if (sticker.OwnerUserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Sticker download forbidden. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                sticker.OwnerUserId,
                currentUser.UserId);
            return Results.NotFound(new ProblemResponse("Sticker was not found."));
        }

        if (sticker.Status != StickerStatus.Ready)
        {
            logger.LogWarning(
                "Sticker download skipped because sticker is not ready. StickerId: {StickerId}. Status: {StickerStatus}.",
                id,
                sticker.Status);
            return Results.Conflict(new ProblemResponse("Sticker is not ready yet."));
        }

        if (string.IsNullOrWhiteSpace(sticker.OutputRelativePath) ||
            !storedFileManager.TryGetFullPath(sticker.OutputRelativePath, out var fullPath) ||
            !File.Exists(fullPath))
        {
            logger.LogWarning("Sticker download skipped because output file was not found. StickerId: {StickerId}.", id);
            return Results.NotFound(new ProblemResponse("Sticker output file was not found."));
        }

        logger.LogInformation("Sticker download started. StickerId: {StickerId}. FullPath: {FullPath}.", id, fullPath);
        return Results.File(fullPath, "video/mp4", $"{id:N}.mp4");
    }

    private static IResult DeleteSticker(
        Guid id,
        IMediaRepository repository,
        IStoredFileManager storedFileManager,
        StickerProcessingCancellationRegistry cancellationRegistry,
        ICurrentUser currentUser,
        ILogger<Program> logger)
    {
        logger.LogInformation("Sticker delete requested. StickerId: {StickerId}.", id);

        var existingSticker = repository.GetSticker(id);
        if (existingSticker is null)
        {
            logger.LogWarning("Sticker delete skipped because sticker was not found. StickerId: {StickerId}.", id);
            return Results.NotFound();
        }

        if (existingSticker.OwnerUserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Sticker delete forbidden. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                existingSticker.OwnerUserId,
                currentUser.UserId);
            return Results.NotFound();
        }

        if (existingSticker.Status == StickerStatus.Processing)
        {
            var cancellationRequested = cancellationRegistry.CancelProcessing(id);
            logger.LogWarning(
                "Sticker delete requested while sticker is processing. StickerId: {StickerId}. Status: {StickerStatus}. CancellationRequested: {CancellationRequested}.",
                id,
                existingSticker.Status,
                cancellationRequested);
        }

        var removedSticker = repository.RemoveSticker(id);
        if (removedSticker is null)
        {
            logger.LogWarning("Sticker delete skipped because remove returned null. StickerId: {StickerId}.", id);
            return Results.NotFound();
        }

        storedFileManager.DeleteStickerOutputFile(removedSticker, logger);
        logger.LogInformation(
            "Sticker deleted from repository. StickerId: {StickerId}. OutputRelativePath: {OutputRelativePath}.",
            id,
            removedSticker.OutputRelativePath);
        return Results.NoContent();
    }

    private static bool HasUsablePreview(MediaFile mediaFile) =>
        mediaFile.Preview?.DurationMs is > 0;

    private static bool IsOutsideMediaDuration(int trimEndMs, MediaFile mediaFile) =>
        mediaFile.Preview?.DurationMs is long durationMs && trimEndMs > durationMs;
}
