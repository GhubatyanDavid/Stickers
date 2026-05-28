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
        logger.LogInformation("Sticker processing worker started.");
        await EnqueueInterruptedStickersAsync(stoppingToken);

        await foreach (var stickerId in queue.ReadAllAsync(stoppingToken))
        {
            await ProcessStickerAsync(stickerId, stoppingToken);
        }
    }

    private async Task EnqueueInterruptedStickersAsync(CancellationToken cancellationToken)
    {
        var stickersToResume = repository.GetStickers()
            .Where(sticker => sticker.Status is StickerStatus.Queued or StickerStatus.Processing)
            .Select(sticker => sticker.Id)
            .ToArray();

        logger.LogInformation("Re-queueing {StickerCount} interrupted stickers.", stickersToResume.Length);
        foreach (var stickerId in stickersToResume)
        {
            await queue.EnqueueAsync(stickerId, cancellationToken);
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
            logger.LogWarning(
                "Sticker {StickerId} failed because source media {SourceMediaId} was not found.",
                sticker.Id,
                sticker.SourceMediaId);
            sticker.MarkFailed("Source media was not found.");
            repository.UpdateSticker(sticker);
            return;
        }

        if (sourceMedia.Kind is not (MediaKind.Video or MediaKind.Image))
        {
            logger.LogWarning(
                "Sticker {StickerId} failed because source media {SourceMediaId} has unsupported kind {MediaKind}.",
                sticker.Id,
                sourceMedia.Id,
                sourceMedia.Kind);
            sticker.MarkFailed("Only video and image source media are supported by this processor.");
            repository.UpdateSticker(sticker);
            return;
        }

        try
        {
            logger.LogInformation(
                "Sticker processing started. StickerId: {StickerId}. SourceMediaId: {SourceMediaId}. SourceKind: {SourceKind}.",
                sticker.Id,
                sourceMedia.Id,
                sourceMedia.Kind);
            sticker.MarkProcessing();
            repository.UpdateSticker(sticker);

            var audioSourceMedia = sticker.AudioSourceMediaId.HasValue
                ? repository.GetMediaFile(sticker.AudioSourceMediaId.Value)
                : null;

            if (sticker.AudioSourceMediaId.HasValue && audioSourceMedia is null)
            {
                logger.LogWarning(
                    "Sticker {StickerId} failed because audio source media {AudioSourceMediaId} was not found.",
                    sticker.Id,
                    sticker.AudioSourceMediaId.Value);
                sticker.MarkFailed("Audio source media was not found.");
                repository.UpdateSticker(sticker);
                return;
            }

            var processedFile = await processor.ProcessStickerAsync(sourceMedia, audioSourceMedia, sticker, cancellationToken);
            sticker.MarkReady(processedFile.RelativePath, processedFile.PublicUrl);
            repository.UpdateSticker(sticker);
            logger.LogInformation(
                "Sticker processing completed. StickerId: {StickerId}. OutputRelativePath: {OutputRelativePath}.",
                sticker.Id,
                processedFile.RelativePath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Sticker {StickerId} failed during processing.", sticker.Id);
            sticker.MarkFailed(exception.Message);
            repository.UpdateSticker(sticker);
        }
    }
}
