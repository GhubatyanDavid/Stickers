using SoundSticker.Domain;

namespace SoundSticker.Contracts;

public sealed record MediaPreviewResponse(
    long? DurationMs,
    int? Width,
    int? Height,
    bool HasAudio,
    string? ThumbnailUrl)
{
    public static MediaPreviewResponse? FromDomain(MediaPreview? preview) =>
        preview is null
            ? null
            : new(
                preview.DurationMs,
                preview.Width,
                preview.Height,
                preview.HasAudio,
                preview.ThumbnailUrl);
}
