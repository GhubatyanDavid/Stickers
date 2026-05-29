using SoundSticker.Domain;
using SoundSticker.Persistence;
using Npgsql;
using Microsoft.Extensions.Options;
using SoundSticker.Options;

namespace SoundSticker.Processing;

public sealed class StickerProcessingWorker(
    StickerProcessingQueue queue,
    StickerProcessingCancellationRegistry cancellationRegistry,
    IOptions<StickerOptions> stickerOptions,
    IMediaRepository repository,
    IStickerProcessor processor,
    ILogger<StickerProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerCount = GetWorkerCount();
        logger.LogInformation("Sticker processing worker started. WorkerCount: {WorkerCount}.", workerCount);
        await EnqueueInterruptedStickersWithRetryAsync(stoppingToken);

        var workers = Enumerable.Range(1, workerCount)
            .Select(workerId => RunWorkerAsync(workerId, stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task RunWorkerAsync(int workerId, CancellationToken stoppingToken)
    {
        logger.LogInformation("Sticker processing worker lane started. WorkerId: {WorkerId}.", workerId);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var stickerId in queue.ReadAllAsync(stoppingToken))
                {
                    logger.LogInformation(
                        "Sticker dequeued for processing. WorkerId: {WorkerId}. StickerId: {StickerId}.",
                        workerId,
                        stickerId);
                    await ProcessStickerAsync(stickerId, workerId, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (NpgsqlException exception)
            {
                logger.LogError(
                    "Sticker processing worker lane database loop failed. WorkerId: {WorkerId}. Message: {MessageText}",
                    workerId,
                    exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Sticker processing worker lane failed. WorkerId: {WorkerId}.", workerId);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task EnqueueInterruptedStickersWithRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await EnqueueInterruptedStickersAsync(cancellationToken);
                return;
            }
            catch (NpgsqlException exception)
            {
                logger.LogError(
                    "Could not re-queue interrupted stickers because PostgreSQL is unavailable. Message: {MessageText}",
                    exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
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

    private async Task ProcessStickerAsync(Guid stickerId, int workerId, CancellationToken cancellationToken)
    {
        using var processingCancellation = cancellationRegistry.BeginProcessing(stickerId, cancellationToken);
        var processingToken = processingCancellation.Token;

        try
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

            logger.LogInformation(
                "Sticker processing started. WorkerId: {WorkerId}. StickerId: {StickerId}. SourceMediaId: {SourceMediaId}. SourceKind: {SourceKind}.",
                workerId,
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

            var processedFile = await processor.ProcessStickerAsync(sourceMedia, audioSourceMedia, sticker, processingToken);
            sticker.MarkReady(processedFile.RelativePath, processedFile.PublicUrl);
            repository.UpdateSticker(sticker);
            logger.LogInformation(
                "Sticker processing completed. WorkerId: {WorkerId}. StickerId: {StickerId}. OutputRelativePath: {OutputRelativePath}.",
                workerId,
                sticker.Id,
                processedFile.RelativePath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (processingToken.IsCancellationRequested)
        {
            logger.LogInformation("Sticker processing canceled. StickerId: {StickerId}.", stickerId);
        }
        catch (NpgsqlException exception)
        {
            logger.LogError(
                "Sticker {StickerId} could not be processed because PostgreSQL is unavailable. Message: {MessageText}",
                stickerId,
                exception.Message);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(
                "Sticker {StickerId} failed because processing timed out. Message: {MessageText}",
                stickerId,
                exception.Message);
            MarkStickerFailed(stickerId, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Sticker {StickerId} failed during processing.", stickerId);
            MarkStickerFailed(stickerId, exception.Message);
        }
        finally
        {
            cancellationRegistry.EndProcessing(stickerId);
        }
    }

    private void MarkStickerFailed(Guid stickerId, string errorMessage)
    {
        try
        {
            var sticker = repository.GetSticker(stickerId);
            if (sticker is not null)
            {
                sticker.MarkFailed(errorMessage);
                repository.UpdateSticker(sticker);
            }
        }
        catch (NpgsqlException dbException)
        {
            logger.LogError(
                "Could not mark sticker {StickerId} as failed because PostgreSQL is unavailable. Message: {MessageText}",
                stickerId,
                dbException.Message);
        }
    }

    private int GetWorkerCount()
    {
        var configuredWorkerCount = stickerOptions.Value.WorkerCount;
        var maxWorkerCount = Math.Max(1, Environment.ProcessorCount);
        return Math.Clamp(configuredWorkerCount, 1, maxWorkerCount);
    }
}
