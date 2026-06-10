using Microsoft.Extensions.Options;
using SoundSticker.Domain;
using SoundSticker.Options;

namespace SoundSticker.FileStorage;

public sealed class StoredFileManager(
    IOptions<StorageOptions> storageOptions,
    IWebHostEnvironment environment) : IStoredFileManager
{
    public void DeleteStickerOutputFile(Sticker sticker, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(sticker.OutputRelativePath))
        {
            DeleteStoredFile(sticker.OutputRelativePath, logger);
            return;
        }

        var predictableOutputPath = Path.Combine(storageOptions.Value.StickersPath, $"{sticker.Id:N}{GetOutputExtension(sticker.OutputFormat)}");
        logger.LogInformation(
            "Sticker output path is empty, deleting predictable processing output path. StickerId: {StickerId}. RelativePath: {RelativePath}.",
            sticker.Id,
            predictableOutputPath);
        DeleteStoredFile(predictableOutputPath, logger);
    }

    public void DeleteStoredFile(string relativePath, ILogger? logger = null)
    {
        if (!TryGetFullPath(relativePath, out var fullPath))
        {
            logger?.LogWarning("Stored file delete blocked because path is outside storage root. RelativePath: {RelativePath}.", relativePath);
            return;
        }

        if (!File.Exists(fullPath))
        {
            logger?.LogWarning("Stored file delete skipped because file does not exist. RelativePath: {RelativePath}. FullPath: {FullPath}.", relativePath, fullPath);
            return;
        }

        try
        {
            File.Delete(fullPath);
            logger?.LogInformation("Stored file deleted. RelativePath: {RelativePath}. FullPath: {FullPath}.", relativePath, fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(
                exception,
                "Stored file delete failed. RelativePath: {RelativePath}. FullPath: {FullPath}.",
                relativePath,
                fullPath);
        }
    }

    public bool TryGetFullPath(string relativePath, out string fullPath)
    {
        var storageRoot = storageOptions.Value.GetResolvedRootPath(environment.ContentRootPath);
        fullPath = Path.GetFullPath(Path.Combine(storageRoot, relativePath));
        var fullStorageRoot = Path.GetFullPath(storageRoot);
        return IsInsideDirectory(fullPath, fullStorageRoot);
    }

    private static bool IsInsideDirectory(string path, string directory)
    {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetOutputExtension(StickerOutputFormat outputFormat) =>
        outputFormat switch
        {
            StickerOutputFormat.Gif => ".gif",
            StickerOutputFormat.Webp => ".webp",
            _ => ".mp4"
        };
}
