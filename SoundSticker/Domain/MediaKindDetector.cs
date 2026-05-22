namespace SoundSticker.Domain;

public static class MediaKindDetector
{
    public static MediaKind From(string fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
            {
                return MediaKind.Gif;
            }

            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return MediaKind.Image;
            }

            if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                return MediaKind.Audio;
            }

            if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return MediaKind.Video;
            }
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".gif" => MediaKind.Gif,
            ".jpg" or ".jpeg" or ".png" or ".webp" => MediaKind.Image,
            ".mp3" or ".wav" or ".m4a" or ".aac" or ".ogg" => MediaKind.Audio,
            ".mp4" or ".mov" or ".webm" or ".mkv" => MediaKind.Video,
            _ => MediaKind.Unknown
        };
    }
}
