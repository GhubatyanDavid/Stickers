namespace SoundSticker.Options;

public sealed class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    public string ExecutablePath { get; set; } = "ffmpeg";
}
