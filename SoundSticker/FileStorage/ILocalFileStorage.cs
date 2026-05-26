using SoundSticker.Domain;

namespace SoundSticker.Storage;

public interface ILocalFileStorage
{
    Task<SavedMediaFile> SaveOriginalAsync(
        IFormFile file,
        MediaKind mediaKind,
        CancellationToken cancellationToken);
}
