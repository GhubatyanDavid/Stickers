using System.Text;
using Microsoft.Extensions.Options;
using SoundSticker.Auth;
using SoundSticker.Contracts;
using SoundSticker.Domain;
using SoundSticker.FileStorage;
using SoundSticker.Options;
using SoundSticker.Persistence;
using SoundSticker.Telegram;

namespace SoundSticker.Endpoints;

public static class TelegramEndpoints
{
    private const long TelegramStaticStickerMaxBytes = 512 * 1024;
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";
    private const string StartPayloadPrefix = "su_";

    public static RouteGroupBuilder MapTelegramEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/telegram/connect", GetTelegramConnect)
            .WithName("GetTelegramConnect")
            .WithSummary("Get Telegram connect link")
            .WithDescription("Returns a Telegram bot start link for connecting the current X-User-Id to a Telegram chat.")
            .Produces<TelegramConnectResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized);

        api.MapPost("/telegram/webhook", ReceiveTelegramWebhook)
            .WithName("ReceiveTelegramWebhook")
            .WithSummary("Receive Telegram bot webhook")
            .WithDescription("Receives Telegram /start payloads and links Telegram chat ids to app user ids.");

        api.MapPost("/stickers/{id:guid}/telegram/send", SendStickerToTelegram)
            .WithName("SendStickerToTelegram")
            .WithSummary("Send sticker to linked Telegram chat")
            .WithDescription("Sends a ready WebP sticker to the current user's linked Telegram chat through Telegram Bot API sendSticker.")
            .Produces<TelegramSendStickerResponse>()
            .Produces<ProblemResponse>(StatusCodes.Status400BadRequest)
            .Produces<ProblemResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemResponse>(StatusCodes.Status404NotFound)
            .Produces<ProblemResponse>(StatusCodes.Status409Conflict);

        return api;
    }

    private static IResult GetTelegramConnect(
        ITelegramLinkRepository telegramLinks,
        IOptions<TelegramOptions> telegramOptions,
        ICurrentUser currentUser)
    {
        var ownerUserId = currentUser.UserId;
        var options = telegramOptions.Value;
        var link = telegramLinks.GetLinkByOwner(ownerUserId);
        var botUsername = NormalizeBotUsername(options.BotUsername);
        var isConfigured = !string.IsNullOrWhiteSpace(options.BotToken) &&
            !string.IsNullOrWhiteSpace(botUsername);
        var startPayload = BuildStartPayload(ownerUserId);
        var startUrl = !string.IsNullOrWhiteSpace(botUsername) && startPayload is not null
            ? $"https://t.me/{botUsername}?start={Uri.EscapeDataString(startPayload)}"
            : null;

        var instructions = startUrl is null
            ? "Configure Telegram:BotUsername, then ask the user to open the bot and send /start with their app user id."
            : "Open the startUrl once from Telegram. After that, stickers can be sent through POST /api/stickers/{id}/telegram/send.";

        return Results.Ok(new TelegramConnectResponse(
            isConfigured,
            link is not null,
            botUsername,
            startUrl,
            link?.Username,
            link?.ConnectedAt,
            instructions));
    }

    private static async Task<IResult> ReceiveTelegramWebhook(
        TelegramWebhookUpdate update,
        ITelegramLinkRepository telegramLinks,
        IOptions<TelegramOptions> telegramOptions,
        TelegramBotService telegramBot,
        HttpContext httpContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var options = telegramOptions.Value;
        if (!IsValidSecretToken(httpContext, options))
        {
            logger.LogWarning("Telegram webhook rejected because secret token did not match.");
            return Results.Unauthorized();
        }

        var message = update.Message;
        if (message?.Text is null)
        {
            return Results.Ok(new { ok = true });
        }

        var ownerUserId = TryGetOwnerUserIdFromStartCommand(message.Text);
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            return Results.Ok(new { ok = true });
        }

        var now = DateTimeOffset.UtcNow;
        telegramLinks.UpsertLink(new TelegramChatLink(
            ownerUserId,
            message.Chat.Id,
            message.From?.Id,
            NormalizeTelegramUsername(message.From?.Username),
            now,
            now));

        logger.LogInformation(
            "Telegram chat linked. OwnerUserId: {OwnerUserId}. ChatId: {ChatId}. TelegramUserId: {TelegramUserId}.",
            ownerUserId,
            message.Chat.Id,
            message.From?.Id);

        await telegramBot.SendMessageAsync(
            message.Chat.Id,
            "Telegram connected. You can now send ready WebP stickers from SoundSticker.",
            cancellationToken);

        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> SendStickerToTelegram(
        Guid id,
        string? emoji,
        IMediaRepository repository,
        ITelegramLinkRepository telegramLinks,
        IStoredFileManager storedFileManager,
        TelegramBotService telegramBot,
        ICurrentUser currentUser,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var ownerUserId = currentUser.UserId;
        var link = telegramLinks.GetLinkByOwner(ownerUserId);
        if (link is null)
        {
            return Results.Conflict(new ProblemResponse("Telegram is not connected for this user. Open GET /api/telegram/connect and start the bot first."));
        }

        var sticker = repository.GetSticker(id);
        if (sticker is null || !CanReadStickerForUser(sticker, ownerUserId))
        {
            return Results.NotFound(new ProblemResponse("Sticker was not found."));
        }

        if (sticker.Status != StickerStatus.Ready)
        {
            return Results.Conflict(new ProblemResponse("Sticker is not ready yet."));
        }

        if (sticker.OutputFormat != StickerOutputFormat.Webp ||
            !string.Equals(Path.GetExtension(sticker.OutputRelativePath), ".webp", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new ProblemResponse("Telegram send requires a ready WebP sticker. Create it with outputFormat=Webp."));
        }

        if (string.IsNullOrWhiteSpace(sticker.OutputRelativePath) ||
            !storedFileManager.TryGetFullPath(sticker.OutputRelativePath, out var fullPath) ||
            !File.Exists(fullPath))
        {
            return Results.NotFound(new ProblemResponse("Sticker output file was not found."));
        }

        var validationError = ValidateTelegramStaticWebp(fullPath);
        if (validationError is not null)
        {
            logger.LogWarning(
                "Telegram sticker validation failed. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. Error: {ValidationError}.",
                id,
                ownerUserId,
                validationError);
            return Results.BadRequest(new ProblemResponse(validationError));
        }

        logger.LogInformation(
            "Sending sticker to Telegram. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. ChatId: {ChatId}.",
            id,
            ownerUserId,
            link.ChatId);

        try
        {
            await telegramBot.SendStickerAsync(link.ChatId, fullPath, emoji, cancellationToken);
        }
        catch (TelegramApiException exception)
        {
            logger.LogWarning(
                exception,
                "Telegram API rejected sticker send. StickerId: {StickerId}. OwnerUserId: {OwnerUserId}. StatusCode: {StatusCode}.",
                id,
                ownerUserId,
                exception.StatusCode);
            return Results.BadRequest(new ProblemResponse($"Telegram sendSticker failed: {exception.Message}"));
        }

        return Results.Ok(new TelegramSendStickerResponse(true, "Sticker sent to Telegram."));
    }

    private static string? ValidateTelegramStaticWebp(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length <= 0)
        {
            return "Telegram sticker WebP file is empty.";
        }

        if (fileInfo.Length > TelegramStaticStickerMaxBytes)
        {
            return $"Telegram static sticker WebP must be at most {TelegramStaticStickerMaxBytes} bytes.";
        }

        if (!TryReadWebpMetadata(fullPath, out var width, out var height, out var isAnimated))
        {
            return "Telegram sticker output is not a readable WebP image.";
        }

        if (isAnimated)
        {
            return "Telegram static sticker must be a static WebP, but this file is animated. Recreate the sticker with the updated WebP exporter.";
        }

        if (width > 512 || height > 512)
        {
            return $"Telegram static sticker WebP must fit inside 512x512. Current size is {width}x{height}.";
        }

        if (width != 512 && height != 512)
        {
            return $"Telegram static sticker WebP must have one side exactly 512px. Current size is {width}x{height}.";
        }

        return null;
    }

    private static bool TryReadWebpMetadata(
        string fullPath,
        out int width,
        out int height,
        out bool isAnimated)
    {
        width = 0;
        height = 0;
        isAnimated = false;

        var bytes = File.ReadAllBytes(fullPath);
        if (bytes.Length < 20 ||
            !HasAscii(bytes, 0, "RIFF") ||
            !HasAscii(bytes, 8, "WEBP"))
        {
            return false;
        }

        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var chunk = Encoding.ASCII.GetString(bytes, offset, 4);
            var chunkSize = ReadUInt32LittleEndian(bytes, offset + 4);
            var dataOffset = offset + 8;
            if (chunkSize > int.MaxValue || dataOffset + chunkSize > bytes.Length)
            {
                return false;
            }

            if (chunk == "ANIM")
            {
                isAnimated = true;
            }
            else if (chunk == "VP8X" && chunkSize >= 10)
            {
                width = 1 + ReadUInt24LittleEndian(bytes, dataOffset + 4);
                height = 1 + ReadUInt24LittleEndian(bytes, dataOffset + 7);
            }
            else if (chunk == "VP8 " && chunkSize >= 10 && HasVp8StartCode(bytes, dataOffset))
            {
                width = ReadUInt16LittleEndian(bytes, dataOffset + 6) & 0x3fff;
                height = ReadUInt16LittleEndian(bytes, dataOffset + 8) & 0x3fff;
            }
            else if (chunk == "VP8L" && chunkSize >= 5 && bytes[dataOffset] == 0x2f)
            {
                width = 1 + (((bytes[dataOffset + 2] & 0x3f) << 8) | bytes[dataOffset + 1]);
                height = 1 + (((bytes[dataOffset + 4] & 0x0f) << 10) | (bytes[dataOffset + 3] << 2) | ((bytes[dataOffset + 2] & 0xc0) >> 6));
            }

            var paddedChunkSize = chunkSize + (chunkSize % 2);
            offset = dataOffset + checked((int)paddedChunkSize);
        }

        return width > 0 && height > 0;
    }

    private static bool HasAscii(byte[] bytes, int offset, string value) =>
        offset + value.Length <= bytes.Length &&
        Encoding.ASCII.GetString(bytes, offset, value.Length) == value;

    private static bool HasVp8StartCode(byte[] bytes, int dataOffset) =>
        dataOffset + 6 <= bytes.Length &&
        bytes[dataOffset + 3] == 0x9d &&
        bytes[dataOffset + 4] == 0x01 &&
        bytes[dataOffset + 5] == 0x2a;

    private static int ReadUInt16LittleEndian(byte[] bytes, int offset) =>
        bytes[offset] | (bytes[offset + 1] << 8);

    private static int ReadUInt24LittleEndian(byte[] bytes, int offset) =>
        bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16);

    private static uint ReadUInt32LittleEndian(byte[] bytes, int offset) =>
        (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));

    private static bool IsValidSecretToken(HttpContext httpContext, TelegramOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.WebhookSecretToken))
        {
            return true;
        }

        return string.Equals(
            httpContext.Request.Headers[SecretTokenHeaderName].ToString(),
            options.WebhookSecretToken,
            StringComparison.Ordinal);
    }

    private static string? TryGetOwnerUserIdFromStartCommand(string text)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
        {
            return null;
        }

        var payload = parts[1].Trim();
        if (payload.StartsWith(StartPayloadPrefix, StringComparison.Ordinal))
        {
            return TryDecodeBase64Url(payload[StartPayloadPrefix.Length..]);
        }

        return payload;
    }

    private static string? BuildStartPayload(string ownerUserId)
    {
        var payload = $"{StartPayloadPrefix}{Base64UrlEncode(ownerUserId)}";
        return payload.Length <= 64 ? payload : null;
    }

    private static string Base64UrlEncode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string? TryDecodeBase64Url(string value)
    {
        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? NormalizeBotUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return username.Trim().TrimStart('@');
    }

    private static string? NormalizeTelegramUsername(string? username) =>
        string.IsNullOrWhiteSpace(username) ? null : username.Trim().TrimStart('@');

    private static bool CanReadStickerForUser(Sticker sticker, string ownerUserId) =>
        sticker.OwnerUserId == ownerUserId ||
        sticker is { IsPublic: true, Status: StickerStatus.Ready };
}
