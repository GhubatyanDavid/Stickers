namespace SoundSticker.Domain;

public sealed class Sticker
{
    private Sticker(
        Guid id,
        Guid sourceMediaId,
        Guid? coverImageId,
        Guid? audioSourceMediaId,
        StickerAudioMode audioMode,
        int trimStartMs,
        int trimEndMs,
        int audioTrimStartMs,
        int audioTrimEndMs,
        StickerStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        SourceMediaId = sourceMediaId;
        CoverImageId = coverImageId;
        AudioSourceMediaId = audioSourceMediaId;
        AudioMode = audioMode;
        TrimStartMs = trimStartMs;
        TrimEndMs = trimEndMs;
        AudioTrimStartMs = audioTrimStartMs;
        AudioTrimEndMs = audioTrimEndMs;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid SourceMediaId { get; }

    public Guid? CoverImageId { get; }

    public Guid? AudioSourceMediaId { get; }

    public StickerAudioMode AudioMode { get; }

    public int TrimStartMs { get; }

    public int TrimEndMs { get; }

    public int AudioTrimStartMs { get; }

    public int AudioTrimEndMs { get; }

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
        Guid sourceMediaId,
        Guid? coverImageId,
        Guid? audioSourceMediaId,
        int trimStartMs,
        int trimEndMs,
        int audioTrimStartMs,
        int audioTrimEndMs,
        StickerAudioMode audioMode) =>
        new(
            id,
            sourceMediaId,
            coverImageId,
            audioSourceMediaId,
            audioMode,
            trimStartMs,
            trimEndMs,
            audioTrimStartMs,
            audioTrimEndMs,
            StickerStatus.Queued,
            DateTimeOffset.UtcNow);

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
