using SoundSticker.Domain;

namespace SoundSticker.Contracts;

public sealed record MediaFileResponse(
    Guid Id,
    string OriginalFileName,
    MediaKind Kind,
    string ContentType,
    long SizeBytes,
    string Url,
    MediaPreviewResponse? Preview,
    DateTimeOffset CreatedAt)
{
    public static MediaFileResponse FromDomain(MediaFile mediaFile) =>
        new(
            mediaFile.Id,
            mediaFile.OriginalFileName,
            mediaFile.Kind,
            mediaFile.ContentType,
            mediaFile.SizeBytes,
            mediaFile.PublicUrl,
            MediaPreviewResponse.FromDomain(mediaFile.Preview),
            mediaFile.CreatedAt);
}
