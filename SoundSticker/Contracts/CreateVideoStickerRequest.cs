using SoundSticker.Domain;

namespace SoundSticker.Contracts;

public sealed record CreateVideoStickerRequest(
    Guid SourceMediaId,
    Guid? CoverImageId,
    int TrimStartMs,
    int TrimEndMs,
    StickerAudioMode AudioMode,
    Guid? AudioSourceMediaId = null,
    int? AudioTrimStartMs = null,
    int? AudioTrimEndMs = null);
