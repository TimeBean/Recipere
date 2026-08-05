namespace Recipere.Presentation.Telegram;

public static class DurationParser
{
    public static int? ParseSeconds(string? durationString)
    {
        if (string.IsNullOrWhiteSpace(durationString))
            return null;

        var parts = durationString.Split(':');
        var seconds = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var value))
                return null;
            seconds = seconds * 60 + value;
        }

        return seconds;
    }
}
