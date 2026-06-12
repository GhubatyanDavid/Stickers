using System.Collections.Concurrent;
using SoundSticker.Domain;

namespace SoundSticker.Persistence;

public sealed class InMemoryTelegramLinkRepository : ITelegramLinkRepository
{
    private readonly ConcurrentDictionary<string, TelegramChatLink> _links = [];

    public void UpsertLink(TelegramChatLink link)
    {
        _links[link.OwnerUserId] = link;
    }

    public TelegramChatLink? GetLinkByOwner(string ownerUserId) =>
        _links.GetValueOrDefault(ownerUserId);
}
