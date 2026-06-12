using System.Text.Json;

namespace SoundSticker.Telegram;

public static class TelegramJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}
