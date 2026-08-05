namespace Recipere.Infrastructure.Repository;

public sealed class YtDlpOptions
{
    public string StoragePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Recipere",
        "downloads");

    public bool AudioOnly { get; set; } = true;

    public string? CookieFromBrowser { get; set; }

    public long MaxUploadBytes { get; set; } = 50L * 1024 * 1024;
}
