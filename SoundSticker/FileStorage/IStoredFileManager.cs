using SoundSticker.Domain;

namespace SoundSticker.FileStorage;

public interface IStoredFileManager
{
    void DeleteStickerOutputFile(Sticker sticker, ILogger logger);

    void DeleteStoredFile(string relativePath, ILogger? logger = null);

    bool TryGetFullPath(string relativePath, out string fullPath);
}
