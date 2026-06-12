namespace SoundSticker.Contracts;

public sealed record TelegramSendStickerResponse(
    bool Sent,
    string Message);
