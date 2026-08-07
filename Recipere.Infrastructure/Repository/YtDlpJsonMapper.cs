using System.Text.Json;
using Recipere.Core.Model;

namespace Recipere.Infrastructure.Repository;

public static class YtDlpJsonMapper
{
    public static Content MapMetadata(JsonDocument document)
    {
        var root = document.RootElement;
        return new Content
        {
            Url = GetString(root, "webpage_url") ?? string.Empty,
            Path = "",
            Title = new Text(GetString(root, "title") ?? "Unknown"),
            WebpageUrl = GetString(root, "webpage_url") ?? string.Empty,
            ThumbnailUrl = GetString(root, "thumbnail") ?? string.Empty,
            Channel = new Channel
            {
                Name = new Text(GetString(root, "channel") ?? "NA"),
                Url = GetString(root, "channel_url") ?? string.Empty
            },
            DurationString = new Text(GetString(root, "duration_string") ?? "0:00")
        };
    }

    public static int? GetDurationSeconds(JsonElement root)
    {
        if (root.TryGetProperty("duration", out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var seconds))
        {
            return (int)seconds;
        }

        return null;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
