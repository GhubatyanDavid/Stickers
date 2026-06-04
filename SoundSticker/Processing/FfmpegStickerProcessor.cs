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
        var options = stickerOptions.Value;
        var sourcePath = GetStoredMediaPath(storageRoot, sourceMedia);

        var stickersDirectory = Path.Combine(storageRoot, storageOptions.Value.StickersPath);
        Directory.CreateDirectory(stickersDirectory);

        var outputFileName = $"{sticker.Id:N}{GetOutputExtension(sticker.OutputFormat)}";
        var outputRelativePath = Path.Combine(storageOptions.Value.StickersPath, outputFileName);
        var outputPath = Path.Combine(storageRoot, outputRelativePath);
        logger.LogInformation(
            "FFmpeg sticker processing command preparing. StickerId: {StickerId}. SourceMediaId: {SourceMediaId}. AudioSourceMediaId: {AudioSourceMediaId}. OutputFormat: {OutputFormat}. Shape: {Shape}. OutputRelativePath: {OutputRelativePath}. TrimStartMs: {TrimStartMs}. DurationMs: {DurationMs}.",
            sticker.Id,
            sourceMedia.Id,
            audioSourceMedia?.Id,
            sticker.OutputFormat,
            sticker.Shape,
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
            startInfo.ArgumentList.Add(GetOutputFps(options).ToString(CultureInfo.InvariantCulture));
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

        if (audioSourceMedia is not null && sticker.OutputFormat == StickerOutputFormat.Mp4)
        {
            startInfo.ArgumentList.Add("-ss");
            startInfo.ArgumentList.Add(ToSeconds(sticker.AudioTrimStartMs));
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(ToSeconds(sticker.AudioDurationMs));
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(GetStoredMediaPath(storageRoot, audioSourceMedia));
        }

        startInfo.ArgumentList.Add("-filter_complex");
        startInfo.ArgumentList.Add(BuildFilterGraph(sticker, options));
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("[v]");

        if (sticker.OutputFormat == StickerOutputFormat.Gif)
        {
            startInfo.ArgumentList.Add("-an");
            startInfo.ArgumentList.Add("-loop");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(ToSeconds(sticker.DurationMs));
        }
        else
        {
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
            startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(options.VideoPreset) ? "ultrafast" : options.VideoPreset);
            startInfo.ArgumentList.Add("-crf");
            startInfo.ArgumentList.Add("30");
            startInfo.ArgumentList.Add("-pix_fmt");
            startInfo.ArgumentList.Add("yuv420p");
            startInfo.ArgumentList.Add("-movflags");
            startInfo.ArgumentList.Add("+faststart");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(ToSeconds(sticker.DurationMs));
            startInfo.ArgumentList.Add("-shortest");
        }

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
                    var reason = timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                        ? "timed out"
                        : "was canceled";
                    logger.LogInformation("Killing FFmpeg process because sticker processing {Reason}. StickerId: {StickerId}.", reason, sticker.Id);
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

        string standardError;
        string standardOutput;
        try
        {
            var standardErrorTask = process.StandardError.ReadToEndAsync(processingToken);
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(processingToken);
            await process.WaitForExitAsync(processingToken);
            standardError = await standardErrorTask;
            standardOutput = await standardOutputTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "FFmpeg timed out for sticker {StickerId} after {ElapsedMs} ms.",
                sticker.Id,
                stopwatch.ElapsedMilliseconds);
            throw new TimeoutException("FFmpeg timed out while processing the sticker.");
        }
        finally
        {
            stopwatch.Stop();
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

    private static string BuildFilterGraph(Sticker sticker, StickerOptions options)
    {
        return sticker.OutputFormat switch
        {
            StickerOutputFormat.Gif => BuildGifFilterGraph(sticker, options),
            _ => BuildMp4FilterGraph(sticker, options)
        };
    }

    private static string BuildMp4FilterGraph(Sticker sticker, StickerOptions options)
    {
        var videoDuration = ToSeconds(sticker.DurationMs);
        var videoFilter = $"[0:v:0]{BuildVisualFilter(sticker, options, allowTransparentMask: false)},format=yuv420p,trim=duration={videoDuration},setpts=PTS-STARTPTS[v]";

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

    private static string BuildGifFilterGraph(Sticker sticker, StickerOptions options)
    {
        var videoDuration = ToSeconds(sticker.DurationMs);

        return
            $"[0:v:0]{BuildVisualFilter(sticker, options, allowTransparentMask: true)},trim=duration={videoDuration},setpts=PTS-STARTPTS,split[gif_frames][palette_source];" +
            "[palette_source]palettegen=stats_mode=diff:reserve_transparent=1[palette];" +
            "[gif_frames][palette]paletteuse=dither=sierra2_4a:alpha_threshold=128[v]";
    }

    private static string BuildVisualFilter(
        Sticker sticker,
        StickerOptions options,
        bool allowTransparentMask)
    {
        var fps = GetOutputFps(options);
        var maxDimension = GetEvenDimension(GetMaxOutputDimension(options));
        var filters = new List<string>
        {
            $"fps={fps}",
            BuildShapeFilter(sticker.Shape, maxDimension),
            "setsar=1"
        };

        if (allowTransparentMask && sticker.RemoveBackground)
        {
            filters.Add("format=rgba");
            filters.Add(BuildBackgroundRemovalFilter(sticker));
        }

        if (sticker.Shape == StickerShape.Circle && allowTransparentMask)
        {
            filters.Add("format=rgba");
            filters.Add(BuildCircleAlphaMask());
        }

        return string.Join(",", filters);
    }

    private static string BuildShapeFilter(StickerShape shape, int maxDimension)
    {
        var (width, height) = GetTargetSize(shape, maxDimension);
        return shape switch
        {
            StickerShape.Original => $"scale={maxDimension}:{maxDimension}:force_original_aspect_ratio=decrease,scale=trunc(iw/2)*2:trunc(ih/2)*2",
            _ => $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}"
        };
    }

    private static (int Width, int Height) GetTargetSize(StickerShape shape, int maxDimension) =>
        shape switch
        {
            StickerShape.Portrait => (GetEvenDimension((int)Math.Round(maxDimension * 4d / 5d)), maxDimension),
            StickerShape.Landscape => (maxDimension, GetEvenDimension((int)Math.Round(maxDimension * 9d / 16d))),
            _ => (maxDimension, maxDimension)
        };

    private static string BuildCircleAlphaMask() =>
        "geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='if(lte(pow(X-W/2,2)+pow(Y-H/2,2),pow(W/2,2)),alpha(X,Y),0)'";

    private static string BuildBackgroundRemovalFilter(Sticker sticker)
    {
        var backgroundColor = string.IsNullOrWhiteSpace(sticker.BackgroundColor)
            ? "0xFFFFFF"
            : sticker.BackgroundColor;
        var similarity = sticker.BackgroundSimilarity.ToString("0.###", CultureInfo.InvariantCulture);
        var blend = sticker.BackgroundBlend.ToString("0.###", CultureInfo.InvariantCulture);
        return $"colorkey={backgroundColor}:{similarity}:{blend}";
    }

    private static string ToSeconds(int milliseconds) =>
        (milliseconds / 1000d).ToString("0.###", CultureInfo.InvariantCulture);

    private TimeSpan GetProcessingTimeout() =>
        TimeSpan.FromSeconds(Math.Max(5, stickerOptions.Value.ProcessingTimeoutSeconds));

    private static int GetOutputFps(StickerOptions options) =>
        Math.Clamp(options.OutputFps, 10, 30);

    private static int GetMaxOutputDimension(StickerOptions options) =>
        Math.Clamp(options.MaxOutputDimension, 128, 1280);

    private static int GetEvenDimension(int dimension) =>
        dimension % 2 == 0 ? dimension : dimension - 1;

    private static string GetOutputExtension(StickerOutputFormat outputFormat) =>
        outputFormat switch
        {
            StickerOutputFormat.Gif => ".gif",
            _ => ".mp4"
        };
}
