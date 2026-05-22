using SoundSticker.Domain;

namespace SoundSticker.Contracts;

public sealed record StickerStatusResponse(
    Guid Id,
    StickerStatus Status,
    string? ErrorMessage,
    string? OutputUrl);
