using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SoundSticker.Domain;
using SoundSticker.Options;

namespace SoundSticker.Processing;

public sealed class FfmpegMediaPreviewAnalyzer(
    IOptions<FfmpegOptions> ffmpegOptions,
    IOptions<StorageOptions> storageOptions,
    IWebHostEnvironment environment,
    ILogger<FfmpegMediaPreviewAnalyzer> logger) : IMediaPreviewAnalyzer
{
    public async Task<MediaPreview?> AnalyzeAsync(MediaFile mediaFile, CancellationToken cancellationToken)
    {
        if (mediaFile.Kind is not (MediaKind.Audio or MediaKind.Gif or MediaKind.Video))
        {
            return null;
        }

        try
        {
            logger.LogInformation(
                "Analyzing media preview. MediaId: {MediaId}. MediaKind: {MediaKind}. RelativePath: {RelativePath}.",
                mediaFile.Id,
                mediaFile.Kind,
                mediaFile.RelativePath);

            var storageRoot = storageOptions.Value.GetResolvedRootPath(environment.ContentRootPath);
            var sourcePath = Path.Combine(storageRoot, mediaFile.RelativePath);
            var probe = await ProbeAsync(sourcePath, cancellationToken);
            var thumbnailUrl = mediaFile.Kind is MediaKind.Gif or MediaKind.Video
                ? await CreateThumbnailAsync(storageRoot, sourcePath, mediaFile.Id, cancellationToken)
                : null;

            var videoStream = probe.Streams.FirstOrDefault(stream => stream.CodecType == "video");
            var preview = new MediaPreview(
                ParseDurationMs(probe.Format?.Duration),
                videoStream?.Width,
                videoStream?.Height,
                probe.Streams.Any(stream => stream.CodecType == "audio"),
                thumbnailUrl);
            logger.LogInformation(
                "Media preview analyzed. MediaId: {MediaId}. DurationMs: {DurationMs}. Width: {Width}. Height: {Height}. HasAudio: {HasAudio}. ThumbnailUrl: {ThumbnailUrl}.",
                mediaFile.Id,
                preview.DurationMs,
                preview.Width,
                preview.Height,
                preview.HasAudio,
                preview.ThumbnailUrl);

            return preview;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not analyze preview metadata for media {MediaId}.", mediaFile.Id);
            return null;
        }
    }

    private async Task<FfprobeOutput> ProbeAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegOptions.Value.ProbeExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format=duration:stream=codec_type,width,height");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(sourcePath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start FFprobe process.");

        var json = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFprobe failed to inspect media. {standardError}");
        }

        return JsonSerializer.Deserialize<FfprobeOutput>(json)
            ?? throw new InvalidOperationException("FFprobe returned empty metadata.");
    }

    private async Task<string?> CreateThumbnailAsync(
        string storageRoot,
        string sourcePath,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var previewsDirectory = Path.Combine(storageRoot, storageOptions.Value.PreviewsPath);
        Directory.CreateDirectory(previewsDirectory);

        var fileName = $"{mediaId:N}.jpg";
        var relativePath = Path.Combine(storageOptions.Value.PreviewsPath, fileName);
        var outputPath = Path.Combine(storageRoot, relativePath);

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
        startInfo.ArgumentList.Add("-frames:v");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add("scale=640:-2:force_original_aspect_ratio=decrease");
        startInfo.ArgumentList.Add(outputPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start FFmpeg thumbnail process.");

        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            logger.LogWarning(
                "FFmpeg could not create thumbnail for media {MediaId}. Error: {Error}",
                mediaId,
                standardError);
            return null;
        }

        var thumbnailUrl = $"{StorageOptions.PublicRequestPath}/{relativePath.Replace('\\', '/')}";
        logger.LogInformation(
            "Media thumbnail created. MediaId: {MediaId}. RelativePath: {RelativePath}.",
            mediaId,
            relativePath);

        return thumbnailUrl;
    }

    private static long? ParseDurationMs(string? duration)
    {
        if (!decimal.TryParse(duration, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        return decimal.ToInt64(decimal.Round(seconds * 1000, MidpointRounding.AwayFromZero));
    }

    private sealed record FfprobeOutput(
        [property: JsonPropertyName("streams")] IReadOnlyList<FfprobeStream> Streams,
        [property: JsonPropertyName("format")] FfprobeFormat? Format);

    private sealed record FfprobeStream(
        [property: JsonPropertyName("codec_type")] string? CodecType,
        [property: JsonPropertyName("width")] int? Width,
        [property: JsonPropertyName("height")] int? Height);

    private sealed record FfprobeFormat(
        [property: JsonPropertyName("duration")] string? Duration);
}
