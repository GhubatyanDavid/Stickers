using SoundSticker.Domain;

namespace SoundSticker.Contracts;

public sealed record StickerStatusResponse(
    Guid Id,
    StickerStatus Status,
    StickerOutputFormat OutputFormat,
    StickerShape Shape,
    string? ErrorMessage,
    string? OutputUrl,
    string? DownloadUrl,
    string? OutputFileName);
