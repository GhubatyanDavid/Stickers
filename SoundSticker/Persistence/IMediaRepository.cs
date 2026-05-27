using SoundSticker.Domain;

namespace SoundSticker.Persistence;

public interface IMediaRepository
{
    void AddMediaFile(MediaFile mediaFile);

    void UpdateMediaFile(MediaFile mediaFile);

    MediaFile? GetMediaFile(Guid id);

    IReadOnlyCollection<MediaFile> GetMediaFiles();

    void AddSticker(Sticker sticker);

    void UpdateSticker(Sticker sticker);

    Sticker? GetSticker(Guid id);

    IReadOnlyCollection<Sticker> GetStickers();

    Sticker? RemoveSticker(Guid id);
}
