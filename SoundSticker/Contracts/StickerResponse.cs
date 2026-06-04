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
    StickerOutputFormat OutputFormat,
    bool IsPublic,
    bool IsFavorite,
    string SourceType,
    string? OutputUrl,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static StickerResponse FromDomain(Sticker sticker, MediaKind sourceKind) =>
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
            sticker.OutputFormat,
            sticker.IsPublic,
            false,
            GetSourceType(sourceKind),
            sticker.OutputUrl,
            sticker.ErrorMessage,
            sticker.CreatedAt,
            sticker.CompletedAt);

    private static string GetSourceType(MediaKind sourceKind) =>
        sourceKind switch
        {
            MediaKind.Image => "image",
            MediaKind.Gif => "gif",
            _ => "video"
        };
}
