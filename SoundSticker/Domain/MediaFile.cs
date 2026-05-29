namespace SoundSticker.Domain;

public sealed class MediaFile
{
    private MediaFile(
        Guid id,
        string originalFileName,
        MediaKind kind,
        string contentType,
        long sizeBytes,
        string relativePath,
        string publicUrl,
        string ownerUserId,
        DateTimeOffset createdAt)
    {
        Id = id;
        OriginalFileName = originalFileName;
        Kind = kind;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        RelativePath = relativePath;
        PublicUrl = publicUrl;
        OwnerUserId = ownerUserId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string OriginalFileName { get; }

    public MediaKind Kind { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public string RelativePath { get; }

    public string PublicUrl { get; }

    public string OwnerUserId { get; }

    public MediaPreview? Preview { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static MediaFile Create(
        Guid id,
        string originalFileName,
        MediaKind kind,
        string? contentType,
        long sizeBytes,
        string relativePath,
        string publicUrl,
        string ownerUserId) =>
        new(
            id,
            Path.GetFileName(originalFileName),
            kind,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            sizeBytes,
            relativePath,
            publicUrl,
            ownerUserId,
            DateTimeOffset.UtcNow);

    internal static MediaFile Restore(
        Guid id,
        string originalFileName,
        MediaKind kind,
        string contentType,
        long sizeBytes,
        string relativePath,
        string publicUrl,
        string ownerUserId,
        MediaPreview? preview,
        DateTimeOffset createdAt)
    {
        var mediaFile = new MediaFile(
            id,
            originalFileName,
            kind,
            contentType,
            sizeBytes,
            relativePath,
            publicUrl,
            ownerUserId,
            createdAt);

        mediaFile.Preview = preview;
        return mediaFile;
    }

    public void SetPreview(MediaPreview preview)
    {
        Preview = preview;
    }
}
