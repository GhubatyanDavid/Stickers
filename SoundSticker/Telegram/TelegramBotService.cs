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

        await SendTelegramRequestAsync(
            "sendMessage",
            BuildTelegramApiUrl(options.BotToken, "sendMessage"),
            content,
            cancellationToken);
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

        await SendTelegramRequestAsync(
            "sendSticker",
            BuildTelegramApiUrl(options.BotToken, "sendSticker"),
            content,
            cancellationToken);

        logger.LogInformation("Telegram sticker sent. ChatId: {ChatId}. StickerPath: {StickerPath}.", chatId, stickerPath);
    }

    private async Task SendTelegramRequestAsync(
        string methodName,
        string requestUri,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = TryDeserializeResponse(responseBody);
            if (response.IsSuccessStatusCode && apiResponse?.Ok == true)
            {
                return;
            }

            logger.LogWarning(
                "Telegram {MethodName} failed. StatusCode: {StatusCode}. Description: {Description}. Body: {Body}",
                methodName,
                (int)response.StatusCode,
                apiResponse?.Description,
                responseBody);

            throw new TelegramApiException(
                (int)response.StatusCode,
                apiResponse?.Description ?? $"Telegram {methodName} failed.",
                responseBody);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Telegram {MethodName} HTTP request failed.", methodName);
            throw new TelegramApiException(
                0,
                $"Telegram {methodName} HTTP request failed: {exception.Message}",
                exception.ToString());
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Telegram {MethodName} request timed out.", methodName);
            throw new TelegramApiException(
                0,
                $"Telegram {methodName} request timed out.",
                exception.ToString());
        }
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

    private static string BuildTelegramApiUrl(string botToken, string methodName) =>
        $"https://api.telegram.org/bot{botToken}/{methodName}";

    private sealed record TelegramApiResponse(bool Ok, string? Description);
}
