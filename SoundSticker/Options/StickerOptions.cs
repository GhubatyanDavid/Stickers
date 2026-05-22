namespace SoundSticker.Options;

public sealed class StickerOptions
{
    public const string SectionName = "Sticker";

    public int MaxDurationMs { get; set; } = 5_000;
}
