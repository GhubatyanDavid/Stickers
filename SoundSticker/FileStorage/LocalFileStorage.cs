using Microsoft.Extensions.Options;
using SoundSticker.Domain;
using SoundSticker.Options;

namespace SoundSticker.FileStorage;

public sealed class LocalFileStorage(
    IOptions<StorageOptions> storageOptions,
    IWebHostEnvironment environment) : ILocalFileStorage
{
    public async Task<SavedMediaFile> SaveOriginalAsync(
        IFormFile file,
        MediaKind mediaKind,
        CancellationToken cancellationToken)
    {
        var storageRoot = storageOptions.Value.GetResolvedRootPath(environment.ContentRootPath);
        var originalsDirectory = Path.Combine(storageRoot, storageOptions.Value.OriginalsPath);
        Directory.CreateDirectory(originalsDirectory);

        var id = Guid.NewGuid();
        var extension = GetSafeExtension(file.FileName, mediaKind);
        var fileName = $"{id:N}{extension}";
        var relativePath = Path.Combine(storageOptions.Value.OriginalsPath, fileName);
        var destinationPath = Path.Combine(storageRoot, relativePath);

        await using var stream = File.Create(destinationPath);
        await file.CopyToAsync(stream, cancellationToken);

        var publicUrl = $"{StorageOptions.PublicRequestPath}/{relativePath.Replace('\\', '/')}";
        return new SavedMediaFile(id, relativePath, publicUrl);
    }

    private static string GetSafeExtension(string? fileName, MediaKind mediaKind)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        return mediaKind switch
        {
            MediaKind.Image => ".jpg",
            MediaKind.Gif => ".gif",
            MediaKind.Audio => ".mp3",
            MediaKind.Video => ".mp4",
            _ => ".bin"
        };
    }
}
