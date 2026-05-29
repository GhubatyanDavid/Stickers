using System.Threading.Channels;

namespace SoundSticker.Processing;

public sealed class StickerProcessingQueue(ILogger<StickerProcessingQueue> logger)
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid stickerId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Sticker enqueue requested. StickerId: {StickerId}.", stickerId);
        return _queue.Writer.WriteAsync(stickerId, cancellationToken);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}
