using SoundSticker.Domain;

namespace SoundSticker.Contracts;

public sealed record StickerResponse(
    Guid Id,
    Guid SourceMediaId,
    Guid? CoverImageId,
    StickerStatus Status,
    StickerAudioMode AudioMode,
    Guid? AudioSourceMediaId,
    int TrimStartMs,
    int TrimEndMs,
    int AudioTrimStartMs,
    int AudioTrimEndMs,
    int DurationMs,
    string? OutputUrl,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static StickerResponse FromDomain(Sticker sticker) =>
        new(
            sticker.Id,
            sticker.SourceMediaId,
            sticker.CoverImageId,
            sticker.Status,
            sticker.AudioMode,
            sticker.AudioSourceMediaId,
            sticker.TrimStartMs,
            sticker.TrimEndMs,
            sticker.AudioTrimStartMs,
            sticker.AudioTrimEndMs,
            sticker.DurationMs,
            sticker.OutputUrl,
            sticker.ErrorMessage,
            sticker.CreatedAt,
            sticker.CompletedAt);
}
