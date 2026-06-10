namespace SoundSticker.Domain;

public sealed class Sticker
{
    private Sticker(
        Guid id,
        string name,
        Guid sourceMediaId,
        Guid? coverImageId,
        Guid? audioSourceMediaId,
        StickerAudioMode audioMode,
        StickerOutputFormat outputFormat,
        StickerShape shape,
        bool removeBackground,
        string? backgroundColor,
        double backgroundSimilarity,
        double backgroundBlend,
        int trimStartMs,
        int trimEndMs,
        int audioTrimStartMs,
        int audioTrimEndMs,
        string ownerUserId,
        bool isPublic,
        StickerStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        SourceMediaId = sourceMediaId;
        CoverImageId = coverImageId;
        AudioSourceMediaId = audioSourceMediaId;
        AudioMode = audioMode;
        OutputFormat = outputFormat;
        Shape = shape;
        RemoveBackground = removeBackground;
        BackgroundColor = backgroundColor;
        BackgroundSimilarity = backgroundSimilarity;
        BackgroundBlend = backgroundBlend;
        TrimStartMs = trimStartMs;
        TrimEndMs = trimEndMs;
        AudioTrimStartMs = audioTrimStartMs;
        AudioTrimEndMs = audioTrimEndMs;
        OwnerUserId = ownerUserId;
        IsPublic = isPublic;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Name { get; }

    public Guid SourceMediaId { get; }

    public Guid? CoverImageId { get; }

    public Guid? AudioSourceMediaId { get; }

    public StickerAudioMode AudioMode { get; }

    public StickerOutputFormat OutputFormat { get; }

    public StickerShape Shape { get; }

    public bool RemoveBackground { get; }

    public string? BackgroundColor { get; }

    public double BackgroundSimilarity { get; }

    public double BackgroundBlend { get; }

    public int TrimStartMs { get; }

    public int TrimEndMs { get; }

    public int AudioTrimStartMs { get; }

    public int AudioTrimEndMs { get; }

    public string OwnerUserId { get; }

    public bool IsPublic { get; private set; }

    public int DurationMs => TrimEndMs - TrimStartMs;

    public int AudioDurationMs => AudioTrimEndMs - AudioTrimStartMs;

    public StickerStatus Status { get; private set; }

    public string? OutputRelativePath { get; private set; }

    public string? OutputUrl { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static Sticker CreateVideoSticker(
        Guid id,
        string name,
        Guid sourceMediaId,
        Guid? coverImageId,
        Guid? audioSourceMediaId,
        int trimStartMs,
        int trimEndMs,
        int audioTrimStartMs,
        int audioTrimEndMs,
        StickerAudioMode audioMode,
        StickerOutputFormat outputFormat,
        StickerShape shape,
        bool removeBackground,
        string? backgroundColor,
        double backgroundSimilarity,
        double backgroundBlend,
        string ownerUserId,
        bool isPublic) =>
        new(
            id,
            name,
            sourceMediaId,
            coverImageId,
            audioSourceMediaId,
            audioMode,
            outputFormat,
            shape,
            removeBackground,
            backgroundColor,
            backgroundSimilarity,
            backgroundBlend,
            trimStartMs,
            trimEndMs,
            audioTrimStartMs,
            audioTrimEndMs,
            ownerUserId,
            isPublic,
            StickerStatus.Queued,
            DateTimeOffset.UtcNow);

    internal static Sticker Restore(
        Guid id,
        string name,
        Guid sourceMediaId,
        Guid? coverImageId,
        Guid? audioSourceMediaId,
        StickerAudioMode audioMode,
        StickerOutputFormat outputFormat,
        StickerShape shape,
        bool removeBackground,
        string? backgroundColor,
        double backgroundSimilarity,
        double backgroundBlend,
        int trimStartMs,
        int trimEndMs,
        int audioTrimStartMs,
        int audioTrimEndMs,
        StickerStatus status,
        string? outputRelativePath,
        string? outputUrl,
        string? errorMessage,
        string ownerUserId,
        bool isPublic,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt)
    {
        var sticker = new Sticker(
            id,
            name,
            sourceMediaId,
            coverImageId,
            audioSourceMediaId,
            audioMode,
            outputFormat,
            shape,
            removeBackground,
            backgroundColor,
            backgroundSimilarity,
            backgroundBlend,
            trimStartMs,
            trimEndMs,
            audioTrimStartMs,
            audioTrimEndMs,
            ownerUserId,
            isPublic,
            status,
            createdAt);

        sticker.OutputRelativePath = outputRelativePath;
        sticker.OutputUrl = outputUrl;
        sticker.ErrorMessage = errorMessage;
        sticker.CompletedAt = completedAt;
        return sticker;
    }

    public void MarkProcessing()
    {
        Status = StickerStatus.Processing;
        ErrorMessage = null;
    }

    public void MarkReady(string outputRelativePath, string outputUrl)
    {
        Status = StickerStatus.Ready;
        OutputRelativePath = outputRelativePath;
        OutputUrl = outputUrl;
        ErrorMessage = null;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = StickerStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
