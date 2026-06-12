namespace SoundSticker.Domain;

public sealed record TelegramChatLink(
    string OwnerUserId,
    long ChatId,
    long? TelegramUserId,
    string? Username,
    DateTimeOffset ConnectedAt,
    DateTimeOffset UpdatedAt);
