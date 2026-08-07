namespace Recipere.Core.Model;

public static class VideoQualitySelector
{
    private static readonly (int Height, int Kbps)[] Rates =
    {
        (1080, 4000),
        (720, 2000),
        (480, 1000),
        (360, 500),
        (240, 300)
    };

    public static IReadOnlyList<VideoQuality> GetFitting(
        long maxUploadBytes,
        IReadOnlyCollection<int> availableHeights,
        IReadOnlyDictionary<int, long?> estimatedSizeByHeight)
    {
        var result = new List<VideoQuality>();
        foreach (var height in availableHeights.Distinct().OrderBy(h => h))
        {
            if (!estimatedSizeByHeight.TryGetValue(height, out var size))
            {
                continue;
            }

            if (size is null)
            {
                result.Add(new VideoQuality(height, $"{height}p"));
            }
            else if (maxUploadBytes <= 0 || size.Value <= maxUploadBytes)
            {
                result.Add(new VideoQuality(height, $"{height}p"));
            }
        }

        return result;
    }

    public static IReadOnlyList<VideoQuality> GetFitting(
        int? durationSeconds,
        long maxUploadBytes,
        IReadOnlyCollection<int>? availableHeights = null)
    {
        var candidates = Rates
            .Where(r => availableHeights is null || availableHeights.Contains(r.Height))
            .OrderBy(r => r.Height)
            .ToList();

        if (durationSeconds is null or <= 0)
        {
            return candidates.Select(r => new VideoQuality(r.Height, $"{r.Height}p")).ToList();
        }

        var budget = maxUploadBytes > 0 ? maxUploadBytes * 0.9 : double.MaxValue;
        var duration = durationSeconds.Value;

        var result = new List<VideoQuality>();
        foreach (var (height, kbps) in candidates)
        {
            var estimatedBytes = kbps / 8.0 * 1000 * duration;
            if (estimatedBytes <= budget)
            {
                result.Add(new VideoQuality(height, $"{height}p"));
            }
        }

        return result;
    }
}
