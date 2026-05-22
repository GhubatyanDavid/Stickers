namespace SoundSticker.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public const string PublicRequestPath = "/media";

    public string RootPath { get; set; } = "storage";

    public string OriginalsPath { get; set; } = "originals";

    public string StickersPath { get; set; } = "stickers";

    public string PreviewsPath { get; set; } = "previews";

    public long MaxUploadBytes { get; set; } = 100 * 1024 * 1024;

    public string GetResolvedRootPath(string contentRootPath) =>
        Path.IsPathRooted(RootPath)
            ? RootPath
            : Path.GetFullPath(Path.Combine(contentRootPath, RootPath));
}
