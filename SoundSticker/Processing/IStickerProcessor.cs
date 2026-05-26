using SoundSticker.Domain;

namespace SoundSticker.Processing;

public interface IStickerProcessor
{
    Task<ProcessedStickerFile> ProcessStickerAsync(
        MediaFile sourceMedia,
        MediaFile? audioSourceMedia,
        Sticker sticker,
        CancellationToken cancellationToken);
}
