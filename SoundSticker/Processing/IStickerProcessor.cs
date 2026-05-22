using SoundSticker.Domain;

namespace SoundSticker.Processing;

public interface IStickerProcessor
{
    Task<ProcessedStickerFile> ProcessVideoStickerAsync(
        MediaFile sourceMedia,
        MediaFile? audioSourceMedia,
        Sticker sticker,
        CancellationToken cancellationToken);
}
