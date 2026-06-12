namespace SoundSticker.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;

    public string BotUsername { get; set; } = string.Empty;

    public string WebhookSecretToken { get; set; } = string.Empty;

    public string DefaultStickerEmoji { get; set; } = "🙂";
}
