using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SoundSticker.Options;

namespace SoundSticker.Telegram;

public sealed class TelegramBotService(
    HttpClient httpClient,
    IOptions<TelegramOptions> telegramOptions,
    ILogger<TelegramBotService> logger)
{
    public async Task SendMessageAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken)
    {
        var options = telegramOptions.Value;
        if (string.IsNullOrWhiteSpace(options.BotToken))
        {
            throw new InvalidOperationException("Telegram bot token is not configured.");
        }

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("chat_id", chatId.ToString(CultureInfo.InvariantCulture)),
            new KeyValuePair<string, string>("text", text)
        ]);

        var requestUri = $"bot{options.BotToken}/sendMessage";
        using var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var apiResponse = TryDeserializeResponse(responseBody);
        if (response.IsSuccessStatusCode && apiResponse?.Ok == true)
        {
            return;
        }

        logger.LogWarning(
            "Telegram sendMessage failed. StatusCode: {StatusCode}. Description: {Description}. Body: {Body}",
            (int)response.StatusCode,
            apiResponse?.Description,
            responseBody);
    }

    public async Task SendStickerAsync(
        long chatId,
        string stickerPath,
        string? emoji,
        CancellationToken cancellationToken)
    {
        var options = telegramOptions.Value;
        if (string.IsNullOrWhiteSpace(options.BotToken))
        {
            throw new InvalidOperationException("Telegram bot token is not configured.");
        }

        await using var stickerStream = File.OpenRead(stickerPath);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(chatId.ToString(CultureInfo.InvariantCulture)), "chat_id" },
            { new StringContent(string.IsNullOrWhiteSpace(emoji) ? options.DefaultStickerEmoji : emoji), "emoji" },
            { new StreamContent(stickerStream), "sticker", Path.GetFileName(stickerPath) }
        };

        var requestUri = $"bot{options.BotToken}/sendSticker";
        using var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        var apiResponse = TryDeserializeResponse(responseBody);
        if (response.IsSuccessStatusCode && apiResponse?.Ok == true)
        {
            logger.LogInformation("Telegram sticker sent. ChatId: {ChatId}. StickerPath: {StickerPath}.", chatId, stickerPath);
            return;
        }

        logger.LogWarning(
            "Telegram sendSticker failed. StatusCode: {StatusCode}. Description: {Description}. Body: {Body}",
            (int)response.StatusCode,
            apiResponse?.Description,
            responseBody);
        throw new InvalidOperationException(apiResponse?.Description ?? "Telegram sendSticker failed.");
    }

    private TelegramApiResponse? TryDeserializeResponse(string responseBody)
    {
        try
        {
            return JsonSerializer.Deserialize<TelegramApiResponse>(responseBody, TelegramJson.SerializerOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Could not parse Telegram API response.");
            return null;
        }
    }

    private sealed record TelegramApiResponse(bool Ok, string? Description);
}
