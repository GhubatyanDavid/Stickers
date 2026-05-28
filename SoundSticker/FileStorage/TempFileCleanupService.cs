using Microsoft.Extensions.Options;
using SoundSticker.Options;

namespace SoundSticker.FileStorage;

public sealed class TempFileCleanupService(
    IOptions<StorageOptions> storageOptions,
    IWebHostEnvironment environment,
    ILogger<TempFileCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(GetCleanupInterval());
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupOnceAsync(stoppingToken);
        }
    }

    private Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        var tempDirectory = GetTempDirectory();
        Directory.CreateDirectory(tempDirectory);

        var threshold = DateTimeOffset.UtcNow.AddHours(-GetMaxAgeHours());
        var scannedFiles = 0;
        var deletedFiles = 0;

        foreach (var file in Directory.EnumerateFiles(tempDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            scannedFiles++;
            if (DeleteIfExpired(file, threshold))
            {
                deletedFiles++;
            }
        }

        DeleteEmptyDirectories(tempDirectory, cancellationToken);
        logger.LogInformation(
            "Temp cleanup completed. Directory: {TempDirectory}. Scanned files: {ScannedFiles}. Deleted files: {DeletedFiles}.",
            tempDirectory,
            scannedFiles,
            deletedFiles);

        return Task.CompletedTask;
    }

    private bool DeleteIfExpired(string file, DateTimeOffset threshold)
    {
        try
        {
            var createdAt = File.GetCreationTimeUtc(file);
            var updatedAt = File.GetLastWriteTimeUtc(file);
            var fileTime = createdAt > updatedAt ? createdAt : updatedAt;

            if (fileTime >= threshold.UtcDateTime)
            {
                return false;
            }

            File.Delete(file);
            logger.LogInformation("Deleted expired temp file {FilePath}.", file);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not delete temp file {FilePath}.", file);
            return false;
        }
    }

    private static void DeleteEmptyDirectories(string rootDirectory, CancellationToken cancellationToken)
    {
        foreach (var directory in Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(directory => directory.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private string GetTempDirectory()
    {
        var rootPath = storageOptions.Value.GetResolvedRootPath(environment.ContentRootPath);
        var tempPath = Path.Combine(rootPath, storageOptions.Value.TempPath);
        var fullRootPath = Path.GetFullPath(rootPath);
        var fullTempPath = Path.GetFullPath(tempPath);

        if (!IsInsideDirectory(fullTempPath, fullRootPath))
        {
            throw new InvalidOperationException("Temp storage path must be inside the storage root.");
        }

        return fullTempPath;
    }

    private int GetMaxAgeHours() =>
        Math.Max(1, storageOptions.Value.TempFileMaxAgeHours);

    private TimeSpan GetCleanupInterval() =>
        TimeSpan.FromHours(Math.Max(1, storageOptions.Value.TempCleanupIntervalHours));

    private static bool IsInsideDirectory(string path, string directory)
    {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
