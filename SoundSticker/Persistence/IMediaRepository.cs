using SoundSticker.Domain;

namespace SoundSticker.Persistence;

public interface IMediaRepository
{
    void AddMediaFile(MediaFile mediaFile);

    MediaFile? GetMediaFile(Guid id);

    IReadOnlyCollection<MediaFile> GetMediaFiles();

    void AddSticker(Sticker sticker);

    Sticker? GetSticker(Guid id);

    IReadOnlyCollection<Sticker> GetStickers();
}
