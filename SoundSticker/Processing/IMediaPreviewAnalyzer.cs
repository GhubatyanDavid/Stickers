using SoundSticker.Domain;

namespace SoundSticker.Processing;

public interface IMediaPreviewAnalyzer
{
    Task<MediaPreview?> AnalyzeAsync(MediaFile mediaFile, CancellationToken cancellationToken);
}
