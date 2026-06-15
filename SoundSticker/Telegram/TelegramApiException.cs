namespace SoundSticker.Telegram;

public sealed class TelegramApiException(
    int statusCode,
    string message,
    string? responseBody = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string? ResponseBody { get; } = responseBody;
}
