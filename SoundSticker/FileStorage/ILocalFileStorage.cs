using SoundSticker.Domain;

namespace SoundSticker.FileStorage;

public interface ILocalFileStorage
{
    Task<SavedMediaFile> SaveOriginalAsync(
        IFormFile file,
        MediaKind mediaKind,
        CancellationToken cancellationToken);
}
