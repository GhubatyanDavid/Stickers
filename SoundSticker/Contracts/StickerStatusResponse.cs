using SoundSticker.Domain;

namespace SoundSticker.Contracts;

public sealed record StickerStatusResponse(
    Guid Id,
    StickerStatus Status,
    StickerOutputFormat OutputFormat,
    string? ErrorMessage,
    string? OutputUrl);
