namespace SoundSticker.Storage;

public sealed record SavedMediaFile(
    Guid Id,
    string RelativePath,
    string PublicUrl);
