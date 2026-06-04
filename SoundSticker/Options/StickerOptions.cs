namespace SoundSticker.Options;

public sealed class StickerOptions
{
    public const string SectionName = "Sticker";

    public int MaxDurationMs { get; set; } = 30_000;

    public int ProcessingTimeoutSeconds { get; set; } = 180;

    public int OutputFps { get; set; } = 24;

    public int MaxOutputDimension { get; set; } = 512;

    public string VideoPreset { get; set; } = "ultrafast";

    public int WorkerCount { get; set; } = 2;
}
