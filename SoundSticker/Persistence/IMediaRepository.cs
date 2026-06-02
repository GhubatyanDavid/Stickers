using SoundSticker.Domain;

namespace SoundSticker.Persistence;

public interface IMediaRepository
{
    void AddMediaFile(MediaFile mediaFile);

    void UpdateMediaFile(MediaFile mediaFile);

    MediaFile? GetMediaFile(Guid id);

    IReadOnlyCollection<MediaFile> GetMediaFiles();

    IReadOnlyCollection<MediaFile> GetMediaFilesByOwner(string ownerUserId);

    void AddSticker(Sticker sticker);

    void UpdateSticker(Sticker sticker);

    Sticker? GetSticker(Guid id);

    IReadOnlyCollection<Sticker> GetStickers();

    IReadOnlyCollection<Sticker> GetStickersByOwner(string ownerUserId);

    IReadOnlyCollection<Sticker> GetVisibleStickersForOwner(string ownerUserId);

    IReadOnlyCollection<Sticker> GetPublicStickers();

    Sticker? RemoveSticker(Guid id);
}
