namespace SoundSticker.Domain;

public sealed record MediaPreview(
    long? DurationMs,
    int? Width,
    int? Height,
    bool HasAudio,
    string? ThumbnailUrl);
