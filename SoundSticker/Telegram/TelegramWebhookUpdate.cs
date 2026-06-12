using System.Text.Json.Serialization;

namespace SoundSticker.Telegram;

public sealed record TelegramWebhookUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramWebhookMessage? Message);

public sealed record TelegramWebhookMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")] TelegramWebhookUser? From,
    [property: JsonPropertyName("chat")] TelegramWebhookChat Chat,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramWebhookUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("username")] string? Username);

public sealed record TelegramWebhookChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string Type);
