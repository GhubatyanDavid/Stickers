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
    StickerShape Shape,
    bool RemoveBackground,
    string? BackgroundColor,
    double BackgroundSimilarity,
    double BackgroundBlend,
    bool IsPublic,
    bool IsFavorite,
    bool IsDelete,
    string SourceType,
    string? OutputUrl,
    string? DownloadUrl,
    string? OutputFileName,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static StickerResponse FromDomain(
        Sticker sticker,
        MediaKind sourceKind,
        bool isFavorite = false,
        bool isDelete = false,
        string? currentUserId = null) =>
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
            sticker.Shape,
            sticker.RemoveBackground,
            sticker.BackgroundColor,
            sticker.BackgroundSimilarity,
            sticker.BackgroundBlend,
            sticker.IsPublic,
            isFavorite,
            isDelete,
            GetSourceType(sourceKind),
            sticker.OutputUrl,
            GetDownloadUrl(sticker, currentUserId),
            GetOutputFileName(sticker),
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

    private static string? GetDownloadUrl(Sticker sticker, string? currentUserId)
    {
        if (sticker.Status != StickerStatus.Ready)
        {
            return null;
        }

        var downloadUrl = $"/api/stickers/{sticker.Id}/download";
        return string.IsNullOrWhiteSpace(currentUserId)
            ? downloadUrl
            : $"{downloadUrl}?userId={Uri.EscapeDataString(currentUserId)}";
    }

    private static string? GetOutputFileName(Sticker sticker) =>
        sticker.Status == StickerStatus.Ready ? $"{sticker.Id:N}{GetOutputExtension(sticker)}" : null;

    private static string GetOutputExtension(Sticker sticker)
    {
        var outputExtension = Path.GetExtension(sticker.OutputRelativePath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(outputExtension))
        {
            return outputExtension.ToLowerInvariant();
        }

        return sticker.OutputFormat switch
        {
            StickerOutputFormat.Gif => ".gif",
            _ => ".mp4"
        };
    }
}
