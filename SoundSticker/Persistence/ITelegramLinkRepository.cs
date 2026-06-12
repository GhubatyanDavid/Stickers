using SoundSticker.Domain;

namespace SoundSticker.Persistence;

public interface ITelegramLinkRepository
{
    void UpsertLink(TelegramChatLink link);

    TelegramChatLink? GetLinkByOwner(string ownerUserId);
}
