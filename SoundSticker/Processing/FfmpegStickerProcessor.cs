using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using SoundSticker.Domain;
using SoundSticker.Options;

namespace SoundSticker.Processing;

public sealed class FfmpegStickerProcessor(
    IOptions<FfmpegOptions> ffmpegOptions,
    IOptions<StorageOptions> storageOptions,
    IWebHostEnvironment environment,
    ILogger<FfmpegStickerProcessor> logger) : IStickerProcessor
{
    public async Task<ProcessedStickerFile> ProcessVideoStickerAsync(
        MediaFile sourceMedia,
        MediaFile? audioSourceMedia,
        Sticker sticker,
        CancellationToken cancellationToken)
    {
        var storageRoot = storageOptions.Value.GetResolvedRootPath(environment.ContentRootPath);
        var sourcePath = GetStoredMediaPath(storageRoot, sourceMedia);

        var stickersDirectory = Path.Combine(storageRoot, storageOptions.Value.StickersPath);
        Directory.CreateDirectory(stickersDirectory);

        var outputFileName = $"{sticker.Id:N}.mp4";
        var outputRelativePath = Path.Combine(storageOptions.Value.StickersPath, outputFileName);
        var outputPath = Path.Combine(storageRoot, outputRelativePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegOptions.Value.ExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);

        if (audioSourceMedia is not null)
        {
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(GetStoredMediaPath(storageRoot, audioSourceMedia));
        }

        startInfo.ArgumentList.Add("-filter_complex");
        startInfo.ArgumentList.Add(BuildFilterGraph(sticker));
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("[v]");

        if (sticker.AudioMode == StickerAudioMode.Mute)
        {
            startInfo.ArgumentList.Add("-an");
        }
        else
        {
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("[a]");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("aac");
            startInfo.ArgumentList.Add("-b:a");
            startInfo.ArgumentList.Add("96k");
        }

        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-preset");
        startInfo.ArgumentList.Add("veryfast");
        startInfo.ArgumentList.Add("-crf");
        startInfo.ArgumentList.Add("28");
        startInfo.ArgumentList.Add("-pix_fmt");
        startInfo.ArgumentList.Add("yuv420p");
        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add(outputPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start FFmpeg process.");

        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            logger.LogWarning(
                "FFmpeg failed for sticker {StickerId}. Exit code: {ExitCode}. Output: {Output}. Error: {Error}",
                sticker.Id,
                process.ExitCode,
                standardOutput,
                standardError);

            throw new InvalidOperationException("FFmpeg failed to process the sticker.");
        }

        var publicUrl = $"{StorageOptions.PublicRequestPath}/{outputRelativePath.Replace('\\', '/')}";
        return new ProcessedStickerFile(outputRelativePath, publicUrl);
    }

    private static string GetStoredMediaPath(string storageRoot, MediaFile mediaFile)
    {
        var path = Path.Combine(storageRoot, mediaFile.RelativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Source media file does not exist.", path);
        }

        return path;
    }

    private static string BuildFilterGraph(Sticker sticker)
    {
        var videoStart = ToSeconds(sticker.TrimStartMs);
        var videoDuration = ToSeconds(sticker.DurationMs);
        var videoFilter = $"[0:v:0]trim=start={videoStart}:duration={videoDuration},setpts=PTS-STARTPTS[v]";

        if (sticker.AudioMode == StickerAudioMode.Mute)
        {
            return videoFilter;
        }

        var audioInputIndex = sticker.AudioMode == StickerAudioMode.UseMedia ? 1 : 0;
        var audioStart = ToSeconds(sticker.AudioTrimStartMs);
        var audioDuration = ToSeconds(sticker.AudioDurationMs);
        var audioFilter =
            $"[{audioInputIndex}:a:0]atrim=start={audioStart}:duration={audioDuration}," +
            $"asetpts=PTS-STARTPTS,apad,atrim=duration={videoDuration}[a]";

        return $"{videoFilter};{audioFilter}";
    }

    private static string ToSeconds(int milliseconds) =>
        (milliseconds / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
}
