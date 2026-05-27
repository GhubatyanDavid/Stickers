using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Npgsql;
using SoundSticker.Contracts;
using SoundSticker.Domain;
using SoundSticker.Options;
using SoundSticker.Persistence;
using SoundSticker.Processing;
using SoundSticker.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                  "http://localhost:8083",
                  "http://127.0.0.1:8083",
                  "https://localhost:8083",
                  "https://127.0.0.1:8083")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<StickerOptions>(builder.Configuration.GetSection(StickerOptions.SectionName));
builder.Services.Configure<FfmpegOptions>(builder.Configuration.GetSection(FfmpegOptions.SectionName));
builder.Services.Configure<PersistenceOptions>(builder.Configuration.GetSection(PersistenceOptions.SectionName));

var persistenceOptions = builder.Configuration
    .GetSection(PersistenceOptions.SectionName)
    .Get<PersistenceOptions>() ?? new PersistenceOptions();

if (persistenceOptions.IsPostgreSql)
{
    var connectionString = builder.Configuration.GetConnectionString(persistenceOptions.ConnectionStringName);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"PostgreSQL persistence is enabled, but ConnectionStrings:{persistenceOptions.ConnectionStringName} is missing.");
    }

    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<PostgreSqlSchemaInitializer>();
    builder.Services.AddSingleton<IMediaRepository, PostgreSqlMediaRepository>();
}
else
{
    builder.Services.AddSingleton<IMediaRepository, InMemoryMediaRepository>();
}

builder.Services.AddSingleton<ILocalFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<StickerProcessingQueue>();
builder.Services.AddSingleton<IMediaPreviewAnalyzer, FfmpegMediaPreviewAnalyzer>();
builder.Services.AddSingleton<IStickerProcessor, FfmpegStickerProcessor>();
builder.Services.AddHostedService<StickerProcessingWorker>();

var app = builder.Build();

app.UseCors();

var storageOptions = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
var storageRootPath = storageOptions.GetResolvedRootPath(app.Environment.ContentRootPath);
Directory.CreateDirectory(storageRootPath);

if (persistenceOptions is { IsPostgreSql: true, AutoCreateSchema: true })
{
    await app.Services.GetRequiredService<PostgreSqlSchemaInitializer>().InitializeAsync();
}


app.UseSwagger();
app.UseSwaggerUI();


app.UseExceptionHandler();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRootPath),
    RequestPath = StorageOptions.PublicRequestPath
});

app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow)))
    .WithName("Health");

var api = app.MapGroup("/api");

api.MapPost("/uploads", UploadMediaAsync)
    .DisableAntiforgery()
    .WithName("UploadMedia");

api.MapGet("/media", (IMediaRepository repository) =>
    Results.Ok(repository.GetMediaFiles().Select(MediaFileResponse.FromDomain)))
    .WithName("ListMedia");

api.MapGet("/media/{id:guid}", (Guid id, IMediaRepository repository) =>
{
    var mediaFile = repository.GetMediaFile(id);
    return mediaFile is null
        ? Results.NotFound()
        : Results.Ok(MediaFileResponse.FromDomain(mediaFile));
})
.WithName("GetMedia");

api.MapGet("/media/{id:guid}/file", (Guid id, IMediaRepository repository, IOptions<StorageOptions> storageOptions) =>
{
    var mediaFile = repository.GetMediaFile(id);
    if (mediaFile is null) return Results.NotFound();

    var rootPath = storageOptions.Value.GetResolvedRootPath(Directory.GetCurrentDirectory());
    var fullPath = Path.Combine(rootPath, mediaFile.RelativePath);

    if (!File.Exists(fullPath)) return Results.NotFound();

    return Results.File(fullPath, mediaFile.ContentType);
})
.WithName("GetMediaFileRaw");

api.MapPost("/stickers/from-video", CreateVideoStickerAsync)
    .WithName("CreateVideoSticker");

api.MapPost("/stickers/from-image", CreateImageStickerAsync)
    .WithName("CreateImageSticker");

api.MapGet("/stickers", (IMediaRepository repository) =>
    Results.Ok(repository.GetStickers().Select(StickerResponse.FromDomain)))
    .WithName("ListStickers");

api.MapGet("/stickers/{id:guid}", (Guid id, IMediaRepository repository) =>
{
    var sticker = repository.GetSticker(id);
    return sticker is null
        ? Results.NotFound()
        : Results.Ok(StickerResponse.FromDomain(sticker));
})
.WithName("GetSticker");

api.MapGet("/stickers/{id:guid}/status", (Guid id, IMediaRepository repository) =>
{
    var sticker = repository.GetSticker(id);
    return sticker is null
        ? Results.NotFound()
        : Results.Ok(new StickerStatusResponse(sticker.Id, sticker.Status, sticker.ErrorMessage, sticker.OutputUrl));
})
.WithName("GetStickerStatus");

api.MapDelete("/stickers/{id:guid}", DeleteSticker)
    .WithName("DeleteSticker");

app.Run();

static async Task<IResult> UploadMediaAsync(
    IFormFile file,
    ILocalFileStorage storage,
    IMediaRepository repository,
    IMediaPreviewAnalyzer previewAnalyzer,
    IOptions<StorageOptions> options,
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

    // ⚙️ Ուղղվեց. Ապահովագրություն, եթե վեբից ContentType-ը կամ FileName-ը սխալ/խառնված են գալիս
    var contentType = file.ContentType ?? "image/jpeg";
    var fileName = file.FileName ?? "file.jpg";

    var mediaKind = MediaKindDetector.From(fileName, contentType);

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
    var mediaFile = MediaFile.Create(
        savedFile.Id,
        fileName,
        mediaKind,
        contentType,
        file.Length,
        savedFile.RelativePath,
        savedFile.PublicUrl);

    repository.AddMediaFile(mediaFile);

    var preview = await previewAnalyzer.AnalyzeAsync(mediaFile, cancellationToken);
    if (preview is not null)
    {
        mediaFile.SetPreview(preview);
        repository.UpdateMediaFile(mediaFile);
    }

    return Results.Created($"/api/media/{mediaFile.Id}", MediaFileResponse.FromDomain(mediaFile));
}

static async Task<IResult> CreateVideoStickerAsync(
    CreateVideoStickerRequest request,
    IMediaRepository repository,
    StickerProcessingQueue queue,
    IOptions<StickerOptions> stickerOptions,
    CancellationToken cancellationToken) =>
    await CreateStickerAsync(
        request,
        repository,
        queue,
        stickerOptions,
        requiredSourceKind: null,
        cancellationToken);

static async Task<IResult> CreateImageStickerAsync(
    CreateVideoStickerRequest request,
    IMediaRepository repository,
    StickerProcessingQueue queue,
    IOptions<StickerOptions> stickerOptions,
    CancellationToken cancellationToken) =>
    await CreateStickerAsync(
        request,
        repository,
        queue,
        stickerOptions,
        MediaKind.Image,
        cancellationToken);

static async Task<IResult> CreateStickerAsync(
    CreateVideoStickerRequest request,
    IMediaRepository repository,
    StickerProcessingQueue queue,
    IOptions<StickerOptions> stickerOptions,
    MediaKind? requiredSourceKind,
    CancellationToken cancellationToken)
{
    var sourceMedia = repository.GetMediaFile(request.SourceMediaId);
    if (sourceMedia is null)
    {
        return Results.NotFound(new ProblemResponse("Source media was not found."));
    }

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

    if (request.CoverImageId.HasValue)
    {
        var coverImage = repository.GetMediaFile(request.CoverImageId.Value);
        if (coverImage is null)
        {
            return Results.BadRequest(new ProblemResponse("Cover image media was not found."));
        }
    }

    if (request.TrimStartMs < 0 || request.TrimEndMs <= request.TrimStartMs)
    {
        return Results.BadRequest(new ProblemResponse("Trim range is invalid."));
    }

    var durationMs = request.TrimEndMs - request.TrimStartMs;
    if (durationMs > stickerOptions.Value.MaxDurationMs)
    {
        return Results.BadRequest(new ProblemResponse($"Sticker can be at most {stickerOptions.Value.MaxDurationMs} ms."));
    }

    if (sourceMedia.Kind == MediaKind.Video && IsOutsideMediaDuration(request.TrimEndMs, sourceMedia))
    {
        return Results.BadRequest(new ProblemResponse("Video trim range exceeds the source video duration."));
    }

    if (!Enum.IsDefined(request.AudioMode))
    {
        return Results.BadRequest(new ProblemResponse("Audio mode is invalid."));
    }

    var audioTrimStartMs = request.AudioTrimStartMs ?? request.TrimStartMs;
    var audioTrimEndMs = request.AudioTrimEndMs ?? request.TrimEndMs;

    if (request.AudioMode != StickerAudioMode.Mute &&
        (audioTrimStartMs < 0 || audioTrimEndMs <= audioTrimStartMs))
    {
        return Results.BadRequest(new ProblemResponse("Audio trim range is invalid."));
    }

    MediaFile? audioSourceMedia = null;
    if (request.AudioMode == StickerAudioMode.UseMedia)
    {
        if (!request.AudioSourceMediaId.HasValue)
        {
            return Results.BadRequest(new ProblemResponse("Audio source media is required for UseMedia mode."));
        }

        audioSourceMedia = repository.GetMediaFile(request.AudioSourceMediaId.Value);
        if (audioSourceMedia is null)
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

        if (IsOutsideMediaDuration(audioTrimEndMs, audioSourceMedia))
        {
            return Results.BadRequest(new ProblemResponse("Audio trim range exceeds the audio source duration."));
        }
    }
    else if (request.AudioSourceMediaId.HasValue)
    {
        return Results.BadRequest(new ProblemResponse("Audio source media can only be used with UseMedia mode."));
    }
    else if (request.AudioMode == StickerAudioMode.KeepOriginal)
    {
        if (sourceMedia.Kind == MediaKind.Image)
        {
            return Results.BadRequest(new ProblemResponse("Image source media does not contain original audio. Use Mute or choose another audio source."));
        }

        if (!sourceMedia.Preview!.HasAudio)
        {
            return Results.BadRequest(new ProblemResponse("Source video does not contain an audio stream. Use Mute or choose another audio source."));
        }

        if (IsOutsideMediaDuration(audioTrimEndMs, sourceMedia))
        {
            return Results.BadRequest(new ProblemResponse("Audio trim range exceeds the source video duration."));
        }
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
        request.AudioMode);

    repository.AddSticker(sticker);
    await queue.EnqueueAsync(sticker.Id, cancellationToken);

    return Results.Accepted($"/api/stickers/{sticker.Id}", StickerResponse.FromDomain(sticker));
}

static bool HasUsablePreview(MediaFile mediaFile) =>
    mediaFile.Preview?.DurationMs is > 0;

static bool IsOutsideMediaDuration(int trimEndMs, MediaFile mediaFile) =>
    mediaFile.Preview?.DurationMs is long durationMs && trimEndMs > durationMs;

static IResult DeleteSticker(
    Guid id,
    IMediaRepository repository,
    IOptions<StorageOptions> storageOptions)
{
    var existingSticker = repository.GetSticker(id);
    if (existingSticker is null)
    {
        return Results.NotFound();
    }

    if (existingSticker.Status == StickerStatus.Processing)
    {
        return Results.Conflict(new ProblemResponse("Sticker is currently processing and cannot be deleted."));
    }

    var removedSticker = repository.RemoveSticker(id);
    if (removedSticker is null)
    {
        return Results.NotFound();
    }

    DeleteStickerOutputFile(removedSticker, storageOptions.Value);
    return Results.NoContent();
}

static void DeleteStickerOutputFile(Sticker sticker, StorageOptions storageOptions)
{
    if (string.IsNullOrWhiteSpace(sticker.OutputRelativePath))
    {
        return;
    }

    var storageRoot = storageOptions.GetResolvedRootPath(Directory.GetCurrentDirectory());
    var fullPath = Path.GetFullPath(Path.Combine(storageRoot, sticker.OutputRelativePath));
    var fullStorageRoot = Path.GetFullPath(storageRoot);

    if (!fullPath.StartsWith(fullStorageRoot, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    if (File.Exists(fullPath))
    {
        File.Delete(fullPath);
    }
}
