namespace SoundSticker.Contracts;

public sealed record TelegramConnectResponse(
    bool IsConfigured,
    bool IsLinked,
    string? BotUsername,
    string? StartUrl,
    string? ConnectedUsername,
    DateTimeOffset? ConnectedAt,
    string Instructions);
