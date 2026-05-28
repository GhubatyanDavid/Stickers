namespace SoundSticker.FileStorage;

public sealed record SavedMediaFile(
    Guid Id,
    string RelativePath,
    string PublicUrl);
