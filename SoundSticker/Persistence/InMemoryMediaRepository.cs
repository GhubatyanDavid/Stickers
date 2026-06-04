using System.Collections.Concurrent;
using SoundSticker.Domain;

namespace SoundSticker.Persistence;

public sealed class InMemoryMediaRepository : IMediaRepository
{
    private readonly ConcurrentDictionary<Guid, MediaFile> _mediaFiles = [];
    private readonly ConcurrentDictionary<Guid, Sticker> _stickers = [];
    private readonly ConcurrentDictionary<(Guid StickerId, string OwnerUserId), byte> _stickerFavorites = [];

    public void AddMediaFile(MediaFile mediaFile)
    {
        _mediaFiles[mediaFile.Id] = mediaFile;
    }

    public void UpdateMediaFile(MediaFile mediaFile)
    {
        _mediaFiles[mediaFile.Id] = mediaFile;
    }

    public MediaFile? GetMediaFile(Guid id) =>
        _mediaFiles.GetValueOrDefault(id);

    public IReadOnlyCollection<MediaFile> GetMediaFiles() =>
        _mediaFiles.Values.OrderByDescending(mediaFile => mediaFile.CreatedAt).ToArray();

    public IReadOnlyCollection<MediaFile> GetMediaFilesByOwner(string ownerUserId) =>
        _mediaFiles.Values
            .Where(mediaFile => mediaFile.OwnerUserId == ownerUserId)
            .OrderByDescending(mediaFile => mediaFile.CreatedAt)
            .ToArray();

    public void AddSticker(Sticker sticker)
    {
        _stickers[sticker.Id] = sticker;
    }

    public void UpdateSticker(Sticker sticker)
    {
        _stickers[sticker.Id] = sticker;
    }

    public Sticker? GetSticker(Guid id) =>
        _stickers.GetValueOrDefault(id);

    public IReadOnlyCollection<Sticker> GetStickers() =>
        _stickers.Values.OrderByDescending(sticker => sticker.CreatedAt).ToArray();

    public IReadOnlyCollection<Sticker> GetStickersByOwner(string ownerUserId) =>
        _stickers.Values
            .Where(sticker => sticker.OwnerUserId == ownerUserId)
            .OrderByDescending(sticker => sticker.CreatedAt)
            .ToArray();

    public IReadOnlyCollection<Sticker> GetVisibleStickersForOwner(string ownerUserId) =>
        _stickers.Values
            .Where(sticker =>
                sticker.OwnerUserId == ownerUserId ||
                (sticker.Status == StickerStatus.Ready && sticker.IsPublic))
            .OrderByDescending(sticker => sticker.CreatedAt)
            .ToArray();

    public IReadOnlyCollection<Sticker> GetPublicStickers() =>
        _stickers.Values
            .Where(sticker => sticker.Status == StickerStatus.Ready && sticker.IsPublic)
            .OrderByDescending(sticker => sticker.CreatedAt)
            .ToArray();

    public Sticker? RemoveSticker(Guid id) =>
        _stickers.TryRemove(id, out var sticker) ? RemoveStickerFavorites(id, sticker) : null;

    public void AddStickerFavorite(Guid stickerId, string ownerUserId)
    {
        _stickerFavorites[(stickerId, ownerUserId)] = 0;
    }

    public void RemoveStickerFavorite(Guid stickerId, string ownerUserId)
    {
        _stickerFavorites.TryRemove((stickerId, ownerUserId), out _);
    }

    public bool IsStickerFavorite(Guid stickerId, string ownerUserId) =>
        _stickerFavorites.ContainsKey((stickerId, ownerUserId));

    public IReadOnlyCollection<Guid> GetFavoriteStickerIdsByOwner(string ownerUserId) =>
        _stickerFavorites.Keys
            .Where(favorite => favorite.OwnerUserId == ownerUserId)
            .Select(favorite => favorite.StickerId)
            .ToHashSet();

    private Sticker RemoveStickerFavorites(Guid id, Sticker sticker)
    {
        foreach (var favorite in _stickerFavorites.Keys.Where(favorite => favorite.StickerId == id).ToArray())
        {
            _stickerFavorites.TryRemove(favorite, out _);
        }

        return sticker;
    }
}
