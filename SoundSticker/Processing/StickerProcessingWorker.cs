using SoundSticker.Domain;
using SoundSticker.Persistence;

namespace SoundSticker.Processing;

public sealed class StickerProcessingWorker(
    StickerProcessingQueue queue,
    IMediaRepository repository,
    IStickerProcessor processor,
    ILogger<StickerProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var stickerId in queue.ReadAllAsync(stoppingToken))
        {
            await ProcessStickerAsync(stickerId, stoppingToken);
        }
    }

    private async Task ProcessStickerAsync(Guid stickerId, CancellationToken cancellationToken)
    {
        var sticker = repository.GetSticker(stickerId);
        if (sticker is null)
        {
            logger.LogWarning("Queued sticker {StickerId} was not found.", stickerId);
            return;
        }

        var sourceMedia = repository.GetMediaFile(sticker.SourceMediaId);
        if (sourceMedia is null)
        {
            sticker.MarkFailed("Source media was not found.");
            return;
        }

        if (sourceMedia.Kind != MediaKind.Video)
        {
            sticker.MarkFailed("Only video source media is supported by this processor.");
            return;
        }

        try
        {
            sticker.MarkProcessing();
            var audioSourceMedia = sticker.AudioSourceMediaId.HasValue
                ? repository.GetMediaFile(sticker.AudioSourceMediaId.Value)
                : null;

            if (sticker.AudioSourceMediaId.HasValue && audioSourceMedia is null)
            {
                sticker.MarkFailed("Audio source media was not found.");
                return;
            }

            var processedFile = await processor.ProcessVideoStickerAsync(sourceMedia, audioSourceMedia, sticker, cancellationToken);
            sticker.MarkReady(processedFile.RelativePath, processedFile.PublicUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Sticker {StickerId} failed during processing.", sticker.Id);
            sticker.MarkFailed(exception.Message);
        }
    }
}
