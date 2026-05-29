using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using SoundSticker.Domain;
using SoundSticker.Options;

namespace SoundSticker.Processing;

public sealed class FfmpegStickerProcessor(
    IOptions<FfmpegOptions> ffmpegOptions,
    IOptions<StickerOptions> stickerOptions,
    IOptions<StorageOptions> storageOptions,
    IWebHostEnvironment environment,
    ILogger<FfmpegStickerProcessor> logger) : IStickerProcessor
{
    public async Task<ProcessedStickerFile> ProcessStickerAsync(
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
        logger.LogInformation(
            "FFmpeg sticker processing command preparing. StickerId: {StickerId}. SourceMediaId: {SourceMediaId}. AudioSourceMediaId: {AudioSourceMediaId}. OutputRelativePath: {OutputRelativePath}. TrimStartMs: {TrimStartMs}. DurationMs: {DurationMs}.",
            sticker.Id,
            sourceMedia.Id,
            audioSourceMedia?.Id,
            outputRelativePath,
            sticker.TrimStartMs,
            sticker.DurationMs);

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegOptions.Value.ExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-nostdin");
        if (sourceMedia.Kind == MediaKind.Image)
        {
            startInfo.ArgumentList.Add("-loop");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-framerate");
            startInfo.ArgumentList.Add("30");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(ToSeconds(sticker.DurationMs));
        }
        else
        {
            startInfo.ArgumentList.Add("-ss");
            startInfo.ArgumentList.Add(ToSeconds(sticker.TrimStartMs));
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(ToSeconds(sticker.DurationMs));
        }

        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);

        if (audioSourceMedia is not null)
        {
            startInfo.ArgumentList.Add("-ss");
            startInfo.ArgumentList.Add(ToSeconds(sticker.AudioTrimStartMs));
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(ToSeconds(sticker.AudioDurationMs));
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(GetStoredMediaPath(storageRoot, audioSourceMedia));
        }

        startInfo.ArgumentList.Add("-filter_complex");
        startInfo.ArgumentList.Add(BuildFilterGraph(sticker, sourceMedia.Kind));
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
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add(ToSeconds(sticker.DurationMs));
        startInfo.ArgumentList.Add("-shortest");
        startInfo.ArgumentList.Add(outputPath);

        using var timeout = new CancellationTokenSource(GetProcessingTimeout());
        using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var processingToken = combinedCancellation.Token;
        var stopwatch = Stopwatch.StartNew();

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start FFmpeg process.");
        using var cancellationRegistration = processingToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    logger.LogInformation("Killing FFmpeg process because sticker processing was canceled. StickerId: {StickerId}.", sticker.Id);
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not kill FFmpeg process for canceled sticker {StickerId}.", sticker.Id);
            }
        });

        var standardErrorTask = process.StandardError.ReadToEndAsync(processingToken);
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(processingToken);
        await process.WaitForExitAsync(processingToken);
        var standardError = await standardErrorTask;
        var standardOutput = await standardOutputTask;
        stopwatch.Stop();

        if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "FFmpeg timed out for sticker {StickerId} after {ElapsedMs} ms.",
                sticker.Id,
                stopwatch.ElapsedMilliseconds);
            throw new TimeoutException("FFmpeg timed out while processing the sticker.");
        }

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
        logger.LogInformation(
            "FFmpeg sticker processing succeeded. StickerId: {StickerId}. OutputRelativePath: {OutputRelativePath}. ElapsedMs: {ElapsedMs}.",
            sticker.Id,
            outputRelativePath,
            stopwatch.ElapsedMilliseconds);

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

    private static string BuildFilterGraph(Sticker sticker, MediaKind sourceKind)
    {
        var videoDuration = ToSeconds(sticker.DurationMs);
        var videoFilter = sourceKind == MediaKind.Image
            ? $"[0:v:0]fps=30,scale=trunc(iw/2)*2:trunc(ih/2)*2,setsar=1,trim=duration={videoDuration},setpts=PTS-STARTPTS[v]"
            : $"[0:v:0]trim=duration={videoDuration},setpts=PTS-STARTPTS[v]";

        if (sticker.AudioMode == StickerAudioMode.Mute)
        {
            return videoFilter;
        }

        var audioInputIndex = sticker.AudioMode == StickerAudioMode.UseMedia ? 1 : 0;
        var audioDuration = ToSeconds(sticker.AudioDurationMs);
        var audioFilter =
            $"[{audioInputIndex}:a:0]atrim=duration={audioDuration}," +
            $"asetpts=PTS-STARTPTS,apad,atrim=duration={videoDuration}[a]";

        return $"{videoFilter};{audioFilter}";
    }

    private static string ToSeconds(int milliseconds) =>
        (milliseconds / 1000d).ToString("0.###", CultureInfo.InvariantCulture);

    private TimeSpan GetProcessingTimeout() =>
        TimeSpan.FromSeconds(Math.Max(5, stickerOptions.Value.ProcessingTimeoutSeconds));
}
