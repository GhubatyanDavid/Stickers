using SoundSticker.Domain;

namespace SoundSticker.Contracts;

public sealed record CreateVideoStickerRequest(
    Guid SourceMediaId,
    Guid? CoverImageId,
    int TrimStartMs,
    int TrimEndMs,
    StickerAudioMode AudioMode,
    StickerOutputFormat OutputFormat = StickerOutputFormat.Mp4,
    StickerShape Shape = StickerShape.Original,
    bool RemoveBackground = false,
    string? BackgroundColor = null,
    double? BackgroundSimilarity = null,
    double? BackgroundBlend = null,
    Guid? AudioSourceMediaId = null,
    int? AudioTrimStartMs = null,
    int? AudioTrimEndMs = null,
    bool IsPublic = false);
