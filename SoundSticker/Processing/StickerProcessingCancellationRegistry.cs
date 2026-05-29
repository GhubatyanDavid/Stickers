using System.Collections.Concurrent;

namespace SoundSticker.Processing;

public sealed class StickerProcessingCancellationRegistry(
    ILogger<StickerProcessingCancellationRegistry> logger)
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeProcessing = new();

    public CancellationTokenSource BeginProcessing(Guid stickerId, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_activeProcessing.TryAdd(stickerId, source))
        {
            logger.LogInformation("Sticker processing cancellation registered. StickerId: {StickerId}.", stickerId);
            return source;
        }

        source.Dispose();
        throw new InvalidOperationException($"Sticker {stickerId} is already processing.");
    }

    public bool CancelProcessing(Guid stickerId)
    {
        if (!_activeProcessing.TryGetValue(stickerId, out var source))
        {
            logger.LogInformation("Sticker processing cancel skipped because no active processor was registered. StickerId: {StickerId}.", stickerId);
            return false;
        }

        logger.LogInformation("Sticker processing cancel requested. StickerId: {StickerId}.", stickerId);
        source.Cancel();
        return true;
    }

    public void EndProcessing(Guid stickerId)
    {
        if (!_activeProcessing.TryRemove(stickerId, out var source))
        {
            return;
        }

        source.Dispose();
        logger.LogInformation("Sticker processing cancellation unregistered. StickerId: {StickerId}.", stickerId);
    }
}
