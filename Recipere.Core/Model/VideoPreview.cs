namespace Recipere.Core.Model;

public sealed record VideoPreview(Content Metadata, IReadOnlyList<VideoQuality> Qualities);
