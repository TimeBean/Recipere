namespace Recipere.Core.Model;

public class Content
{
    public required string Url { get; set; }
    public required Text Title { get; set; }
    public required string Path { get; set; }
    public required string WebpageUrl { get; set; }
    public required string ThumbnailUrl { get; set; }
    public required Channel Channel { get; set; }
    public required Text DurationString { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Extension { get; set; }
    public long? SizeBytes { get; set; }
}