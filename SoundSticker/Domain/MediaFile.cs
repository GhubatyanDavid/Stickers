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
        DateTimeOffset createdAt)
    {
        Id = id;
        OriginalFileName = originalFileName;
        Kind = kind;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        RelativePath = relativePath;
        PublicUrl = publicUrl;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string OriginalFileName { get; }

    public MediaKind Kind { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public string RelativePath { get; }

    public string PublicUrl { get; }

    public MediaPreview? Preview { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static MediaFile Create(
        Guid id,
        string originalFileName,
        MediaKind kind,
        string? contentType,
        long sizeBytes,
        string relativePath,
        string publicUrl) =>
        new(
            id,
            Path.GetFileName(originalFileName),
            kind,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            sizeBytes,
            relativePath,
            publicUrl,
            DateTimeOffset.UtcNow);

    internal static MediaFile Restore(
        Guid id,
        string originalFileName,
        MediaKind kind,
        string contentType,
        long sizeBytes,
        string relativePath,
        string publicUrl,
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
            createdAt);

        mediaFile.Preview = preview;
        return mediaFile;
    }

    public void SetPreview(MediaPreview preview)
    {
        Preview = preview;
    }
}
