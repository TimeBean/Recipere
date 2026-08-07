using Recipere.Core.Model;

namespace Recipere.Infrastructure.Repository;

public static class YtDlpMetadataMapper
{
    public const string MetadataPrintTemplate =
        "%(title)s\\t%(webpage_url)s\\t%(thumbnail)s\\t%(channel)s\\t%(channel_url)s\\t%(duration_string)s";

    public static Content Map(string output)
    {
        var parts = output.Split("\\t");
        if (parts.Length != 6)
        {
            throw new InvalidOperationException($"Unexpected yt-dlp output format. Got {parts.Length} parts.");
        }

        return new Content
        {
            Url = parts[1],
            Path = "",
            Title = new Text(parts[0]),
            WebpageUrl = parts[1],
            ThumbnailUrl = parts[2],
            Channel = new Channel
            {
                Name = new Text(parts[3]),
                Url = parts[4]
            },
            DurationString = new Text(parts[5])
        };
    }
}
