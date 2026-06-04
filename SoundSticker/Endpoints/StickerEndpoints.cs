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
            .WithName("CreateVideoSticker")
            .WithSummary("Create video sticker")
            .WithDescription("Queues sticker processing from an uploaded video, GIF, or image. Set outputFormat=Gif for silent GIF output. Use shape to export square, circle, portrait, or landscape stickers. Set isPublic=true to show it in All Stickers after processing.")
            .Accepts<CreateVideoStickerRequest>("application/json")
            .Produces<StickerResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemResponse>(StatusCodes.Status400BadRequest)
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound)
            .Produces<ProblemResponse>(StatusCodes.Status429TooManyRequests);

        api.MapPost("/stickers/from-image", CreateImageStickerAsync)
            .RequireRateLimiting("sticker-creation")
            .WithName("CreateImageSticker")
            .WithSummary("Create image sticker")
            .WithDescription("Queues sticker processing from an uploaded image by looping it into MP4 or GIF. Use shape to export square, circle, portrait, or landscape stickers. Use Mute or UseMedia audio mode.")
            .Accepts<CreateVideoStickerRequest>("application/json")
            .Produces<StickerResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemResponse>(StatusCodes.Status400BadRequest)
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound)
            .Produces<ProblemResponse>(StatusCodes.Status429TooManyRequests);

        api.MapGet("/stickers/my", ListMyStickers)
            .WithName("ListMyStickers")
            .WithSummary("List my stickers")
            .WithDescription("Returns private and public stickers owned by the current X-User-Id.")
            .Produces<StickerResponse[]>()
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

        api.MapGet("/stickers", ListVisibleStickers)
            .WithName("ListStickers")
            .WithSummary("List visible stickers")
            .WithDescription("Returns stickers owned by the current X-User-Id plus ready public stickers from any user.")
            .Produces<StickerResponse[]>()
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

        api.MapGet("/stickers/all", ListAllStickers)
            .WithName("ListAllStickers")
            .WithSummary("List public stickers")
            .WithDescription("Public endpoint. Returns ready stickers created with isPublic=true.")
            .Produces<StickerResponse[]>();

        api.MapGet("/stickers/{id:guid}", GetSticker)
            .WithName("GetSticker")
            .WithSummary("Get visible sticker")
            .WithDescription("Returns one sticker if it belongs to the current X-User-Id or is ready and public.")
            .Produces<StickerResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

        api.MapGet("/stickers/{id:guid}/status", GetStickerStatus)
            .WithName("GetStickerStatus")
            .WithSummary("Get visible sticker status")
            .WithDescription("Returns status, output URL, and error message for one sticker owned by the current X-User-Id or ready and public.")
            .Produces<StickerStatusResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

        api.MapGet("/stickers/{id:guid}/download", DownloadSticker)
            .WithName("DownloadSticker")
            .WithSummary("Download visible sticker output")
            .WithDescription("Downloads the generated MP4 or GIF when the sticker is owned by the current X-User-Id or ready and public.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound)
            .Produces<ProblemResponse>(StatusCodes.Status409Conflict);

        api.MapPost("/stickers/{id:guid}/favorite", FavoriteSticker)
            .WithName("FavoriteSticker")
            .WithSummary("Favorite visible sticker")
            .WithDescription("Marks a visible sticker as favorite for the current X-User-Id.")
            .Produces<StickerFavoriteResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound);

        api.MapDelete("/stickers/{id:guid}/favorite", UnfavoriteSticker)
            .WithName("UnfavoriteSticker")
            .WithSummary("Unfavorite visible sticker")
            .WithDescription("Removes a visible sticker from the current X-User-Id favorites.")
            .Produces<StickerFavoriteResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound);

        api.MapDelete("/stickers/{id:guid}", DeleteSticker)
            .WithName("DeleteSticker")
            .WithSummary("Delete my sticker")
            .WithDescription("Deletes a sticker owned by the current X-User-Id. Returns isDelete=true when deleted and false when the sticker is missing or belongs to another user.")
            .Produces<DeleteStickerResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

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
        var favoriteStickerIds = repository.GetFavoriteStickerIdsByOwner(ownerUserId).ToHashSet();
        var stickers = repository.GetStickersByOwner(ownerUserId).Select(sticker => ToStickerResponse(sticker, repository, ownerUserId, favoriteStickerIds)).ToArray();
        logger.LogInformation("My sticker list returned. OwnerUserId: {OwnerUserId}. Count: {StickerCount}.", ownerUserId, stickers.Length);
        return Results.Ok(stickers);
    }

    private static IResult ListVisibleStickers(IMediaRepository repository, ICurrentUser currentUser, ILogger<Program> logger)
    {
        var ownerUserId = currentUser.UserId;
        logger.LogInformation("Visible sticker list requested. OwnerUserId: {OwnerUserId}.", ownerUserId);
        var favoriteStickerIds = repository.GetFavoriteStickerIdsByOwner(ownerUserId).ToHashSet();
        var stickers = repository.GetVisibleStickersForOwner(ownerUserId).Select(sticker => ToStickerResponse(sticker, repository, ownerUserId, favoriteStickerIds)).ToArray();
        logger.LogInformation("Visible sticker list returned. OwnerUserId: {OwnerUserId}. Count: {StickerCount}.", ownerUserId, stickers.Length);
        return Results.Ok(stickers);
    }

    private static IResult ListAllStickers(IMediaRepository repository, ILogger<Program> logger)
    {
        logger.LogInformation("All public sticker list requested.");
        var stickers = repository.GetPublicStickers().Select(sticker => ToStickerResponse(sticker, repository, currentUserId: null)).ToArray();
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

        if (!CanReadSticker(sticker, currentUser, out var requestUserId))
        {
            logger.LogWarning(
                "Sticker request forbidden. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                sticker.OwnerUserId,
                requestUserId);
            return Results.NotFound();
        }

        logger.LogInformation("Sticker returned. StickerId: {StickerId}. Status: {StickerStatus}. OutputUrl: {OutputUrl}.", id, sticker.Status, sticker.OutputUrl);
        return Results.Ok(ToStickerResponse(sticker, repository, requestUserId));
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

        if (!CanReadSticker(sticker, currentUser, out var requestUserId))
        {
            logger.LogWarning(
                "Sticker status forbidden. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                sticker.OwnerUserId,
                requestUserId);
            return Results.NotFound();
        }

        logger.LogInformation(
            "Sticker status returned. StickerId: {StickerId}. Status: {StickerStatus}. OutputUrl: {OutputUrl}. ErrorMessage: {ErrorMessage}.",
            id,
            sticker.Status,
            sticker.OutputUrl,
            sticker.ErrorMessage);
        return Results.Ok(new StickerStatusResponse(sticker.Id, sticker.Status, sticker.OutputFormat, sticker.Shape, sticker.ErrorMessage, sticker.OutputUrl));
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
            "Sticker creation requested. SourceMediaId: {SourceMediaId}. CoverImageId: {CoverImageId}. AudioSourceMediaId: {AudioSourceMediaId}. AudioMode: {AudioMode}. OutputFormat: {OutputFormat}. Shape: {Shape}. RemoveBackground: {RemoveBackground}. TrimStartMs: {TrimStartMs}. TrimEndMs: {TrimEndMs}. IsPublic: {IsPublic}.",
            request.SourceMediaId,
            request.CoverImageId,
            request.AudioSourceMediaId,
            request.AudioMode,
            request.OutputFormat,
            request.Shape,
            request.RemoveBackground,
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
            request.OutputFormat,
            request.Shape,
            request.RemoveBackground,
            NormalizeBackgroundColor(request.BackgroundColor),
            GetBackgroundSimilarity(request.BackgroundSimilarity),
            GetBackgroundBlend(request.BackgroundBlend),
            ownerUserId,
            request.IsPublic);

        repository.AddSticker(sticker);
        await queue.EnqueueAsync(sticker.Id, cancellationToken);
        logger.LogInformation(
            "Sticker queued. StickerId: {StickerId}. SourceMediaId: {SourceMediaId}. IsPublic: {IsPublic}.",
            sticker.Id,
            sticker.SourceMediaId,
            sticker.IsPublic);

        return Results.Accepted($"/api/stickers/{sticker.Id}", StickerResponse.FromDomain(sticker, sourceMedia.Kind, isDelete: true));
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

        if (sourceMedia.Kind is not (MediaKind.Video or MediaKind.Image or MediaKind.Gif))
        {
            return Results.BadRequest(new ProblemResponse("Source media must be a video, GIF, or image."));
        }

        if (sourceMedia.Kind is (MediaKind.Video or MediaKind.Gif) && !HasUsablePreview(sourceMedia))
        {
            return Results.BadRequest(new ProblemResponse("Source video or GIF preview metadata is unavailable. Check FFprobe and upload the file again."));
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

        if (sourceMedia.Kind is (MediaKind.Video or MediaKind.Gif) && IsOutsideMediaDuration(request.TrimEndMs, sourceMedia))
        {
            return Results.BadRequest(new ProblemResponse("Trim range exceeds the source video or GIF duration."));
        }

        if (!Enum.IsDefined(request.AudioMode))
        {
            return Results.BadRequest(new ProblemResponse("Audio mode is invalid."));
        }

        if (!Enum.IsDefined(request.OutputFormat))
        {
            return Results.BadRequest(new ProblemResponse("Output format is invalid."));
        }

        if (!Enum.IsDefined(request.Shape))
        {
            return Results.BadRequest(new ProblemResponse("Sticker shape is invalid."));
        }

        if (request.OutputFormat == StickerOutputFormat.Gif && request.AudioMode != StickerAudioMode.Mute)
        {
            return Results.BadRequest(new ProblemResponse("GIF output does not support audio. Use Mute audio mode."));
        }

        if (request.Shape == StickerShape.Circle && request.OutputFormat != StickerOutputFormat.Gif)
        {
            return Results.BadRequest(new ProblemResponse("Circle shape requires GIF output because MP4 does not support transparent sticker edges."));
        }

        if (request.RemoveBackground)
        {
            if (sourceMedia.Kind is not (MediaKind.Image or MediaKind.Gif))
            {
                return Results.BadRequest(new ProblemResponse("Background removal is currently supported only for image and GIF sources."));
            }

            if (request.OutputFormat != StickerOutputFormat.Gif)
            {
                return Results.BadRequest(new ProblemResponse("Background removal requires GIF output because MP4 does not support transparent sticker backgrounds."));
            }

            if (!IsValidBackgroundColor(request.BackgroundColor))
            {
                return Results.BadRequest(new ProblemResponse("Background color must be a hex color like #ffffff or 0xffffff."));
            }
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

        if (!CanReadSticker(sticker, currentUser, out var requestUserId))
        {
            logger.LogWarning(
                "Sticker download forbidden. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                sticker.OwnerUserId,
                requestUserId);
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
        return Results.File(fullPath, GetOutputContentType(sticker.OutputFormat), $"{id:N}{GetOutputExtension(sticker.OutputFormat)}");
    }

    private static IResult FavoriteSticker(
        Guid id,
        IMediaRepository repository,
        ICurrentUser currentUser,
        ILogger<Program> logger)
    {
        var ownerUserId = currentUser.UserId;
        logger.LogInformation("Sticker favorite requested. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}.", id, ownerUserId);

        var sticker = repository.GetSticker(id);
        if (sticker is null || !CanReadStickerForUser(sticker, ownerUserId))
        {
            logger.LogWarning("Sticker favorite skipped because sticker was not visible. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}.", id, ownerUserId);
            return Results.NotFound(new ProblemResponse("Sticker was not found."));
        }

        repository.AddStickerFavorite(id, ownerUserId);
        logger.LogInformation("Sticker favorited. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}.", id, ownerUserId);
        return Results.Ok(new StickerFavoriteResponse(true));
    }

    private static IResult UnfavoriteSticker(
        Guid id,
        IMediaRepository repository,
        ICurrentUser currentUser,
        ILogger<Program> logger)
    {
        var ownerUserId = currentUser.UserId;
        logger.LogInformation("Sticker unfavorite requested. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}.", id, ownerUserId);

        var sticker = repository.GetSticker(id);
        if (sticker is null || !CanReadStickerForUser(sticker, ownerUserId))
        {
            logger.LogWarning("Sticker unfavorite skipped because sticker was not visible. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}.", id, ownerUserId);
            return Results.NotFound(new ProblemResponse("Sticker was not found."));
        }

        repository.RemoveStickerFavorite(id, ownerUserId);
        logger.LogInformation("Sticker unfavorited. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}.", id, ownerUserId);
        return Results.Ok(new StickerFavoriteResponse(false));
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
            return Results.Ok(new DeleteStickerResponse(false));
        }

        if (existingSticker.OwnerUserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Sticker delete forbidden. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. RequestUserId: {RequestUserId}.",
                id,
                existingSticker.OwnerUserId,
                currentUser.UserId);
            return Results.Ok(new DeleteStickerResponse(false));
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
            return Results.Ok(new DeleteStickerResponse(false));
        }

        storedFileManager.DeleteStickerOutputFile(removedSticker, logger);
        logger.LogInformation(
            "Sticker deleted from repository. StickerId: {StickerId}. OutputRelativePath: {OutputRelativePath}.",
            id,
            removedSticker.OutputRelativePath);
        return Results.Ok(new DeleteStickerResponse(true));
    }

    private static bool HasUsablePreview(MediaFile mediaFile) =>
        mediaFile.Preview?.DurationMs is > 0;

    private static string GetOutputContentType(StickerOutputFormat outputFormat) =>
        outputFormat switch
        {
            StickerOutputFormat.Gif => "image/gif",
            _ => "video/mp4"
        };

    private static string GetOutputExtension(StickerOutputFormat outputFormat) =>
        outputFormat switch
        {
            StickerOutputFormat.Gif => ".gif",
            _ => ".mp4"
        };

    private static bool IsOutsideMediaDuration(int trimEndMs, MediaFile mediaFile) =>
        mediaFile.Preview?.DurationMs is long durationMs && trimEndMs > durationMs;

    private static bool IsValidBackgroundColor(string? color) =>
        string.IsNullOrWhiteSpace(color) ||
        NormalizeBackgroundColor(color) is not null;

    private static string? NormalizeBackgroundColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var normalized = color.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }
        else if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        return normalized.Length == 6 && normalized.All(Uri.IsHexDigit)
            ? $"0x{normalized.ToUpperInvariant()}"
            : null;
    }

    private static double GetBackgroundSimilarity(double? similarity) =>
        Math.Clamp(similarity ?? 0.18d, 0.01d, 1d);

    private static double GetBackgroundBlend(double? blend) =>
        Math.Clamp(blend ?? 0.08d, 0d, 1d);

    private static bool CanReadSticker(Sticker sticker, ICurrentUser currentUser, out string? requestUserId)
    {
        if (TryGetRequestUserId(currentUser, out requestUserId) && requestUserId is not null)
        {
            return CanReadStickerForUser(sticker, requestUserId);
        }

        if (sticker is { IsPublic: true, Status: StickerStatus.Ready })
        {
            return true;
        }

        throw new MissingUserIdException();
    }

    private static bool CanReadStickerForUser(Sticker sticker, string ownerUserId) =>
        sticker.OwnerUserId == ownerUserId ||
        sticker is { IsPublic: true, Status: StickerStatus.Ready };

    private static bool TryGetRequestUserId(ICurrentUser currentUser, out string? requestUserId)
    {
        try
        {
            requestUserId = currentUser.UserId;
            return true;
        }
        catch (MissingUserIdException)
        {
            requestUserId = null;
            return false;
        }
    }

    private static StickerResponse ToStickerResponse(
        Sticker sticker,
        IMediaRepository repository,
        string? currentUserId,
        IReadOnlySet<Guid>? favoriteStickerIds = null)
    {
        var sourceKind = repository.GetMediaFile(sticker.SourceMediaId)?.Kind ?? MediaKind.Video;
        var isFavorite = currentUserId is not null &&
            (favoriteStickerIds?.Contains(sticker.Id) ?? repository.IsStickerFavorite(sticker.Id, currentUserId));
        return StickerResponse.FromDomain(sticker, sourceKind, isFavorite, sticker.OwnerUserId == currentUserId);
    }
}
