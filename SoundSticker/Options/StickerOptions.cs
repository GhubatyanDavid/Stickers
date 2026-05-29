namespace SoundSticker.Options;

public sealed class StickerOptions
{
    public const string SectionName = "Sticker";

    public int MaxDurationMs { get; set; } = 30_000;

    public int ProcessingTimeoutSeconds { get; set; } = 45;
}
