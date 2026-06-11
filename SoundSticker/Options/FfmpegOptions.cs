namespace SoundSticker.Options;

public sealed class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    public string ExecutablePath { get; set; } = "ffmpeg";

    public string ProbeExecutablePath { get; set; } = "ffprobe";

    public string CwebpExecutablePath { get; set; } = "cwebp";

    public string Img2WebpExecutablePath { get; set; } = "img2webp";
}
