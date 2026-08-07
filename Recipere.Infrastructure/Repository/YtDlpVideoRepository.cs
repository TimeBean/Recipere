using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Recipere.Core.Model;
using Recipere.Core.Repository;

namespace Recipere.Infrastructure.Repository;

public sealed class YtDlpVideoRepository : IVideoRepository
{
    private static string DefaultStoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Recipere",
        "downloads");

    private readonly ILogger<YtDlpVideoRepository> _logger;
    private readonly YtDlpOptions _options;
    private readonly IVideoStorage _videoStorage;
    private readonly YtDlpProcessRunner _runner;
    private readonly string _storagePath;

    public YtDlpVideoRepository(
        IOptions<YtDlpOptions> options,
        IVideoStorage videoStorage,
        ILogger<YtDlpVideoRepository> logger)
    {
        _options = options.Value;
        _videoStorage = videoStorage;
        _logger = logger;
        _runner = new YtDlpProcessRunner(logger);
        _storagePath = string.IsNullOrWhiteSpace(_options.StoragePath)
            ? DefaultStoragePath
            : _options.StoragePath;
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<Content> GetAsync(string url, int maxHeight, CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(url, cancellationToken);

        var (actualPath, width, height) = await DownloadAsync(url, GetUniqueTempPath(), maxHeight, cancellationToken);

        if (actualPath.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
            || File.Exists(actualPath + ".part"))
        {
            TryDeleteFile(actualPath);
            throw new YtDlpException($"Download resulted in .part file: {actualPath}");
        }

        var fileInfo = new FileInfo(actualPath);
        if (fileInfo.Length > _options.MaxUploadBytes)
        {
            TryDeleteFile(actualPath);
            throw new YtDlpException(
                $"The resulting file is too big for Telegram: {fileInfo.Length:N0} bytes exceeds the {_options.MaxUploadBytes:N0}-byte limit.");
        }

        try
        {
            metadata.Path = await _videoStorage.SaveAsync(actualPath, cancellationToken);
        }
        finally
        {
            TryDeleteFile(actualPath);
        }

        metadata.Width = width;
        metadata.Height = height;
        metadata.Extension = Path.GetExtension(actualPath).TrimStart('.').ToLowerInvariant();
        metadata.SizeBytes = fileInfo.Length;

        return metadata;
    }

    public Task RemoveAsync(string handle, CancellationToken cancellationToken = default) =>
        _videoStorage.DeleteAsync(handle, cancellationToken);

    public Task<bool> HasAsync(string handle, CancellationToken cancellationToken = default) =>
        _videoStorage.ContainsAsync(handle, cancellationToken);

    public async Task<Content> GetMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
            $"{GetCookieArg()} --skip-download --print \"{YtDlpMetadataMapper.MetadataPrintTemplate}\" \"{url}\"",
            cancellationToken);

        if (result.ExitCode != 0) throw new YtDlpException($"yt-dlp metadata fetch failed.{DescribeError(result)}");

        return YtDlpMetadataMapper.Map(result.Output);
    }

    public async Task<VideoPreview> PreviewAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
            $"{GetCookieArg()} -J --no-warnings \"{url}\"",
            cancellationToken);

        if (result.ExitCode != 0) throw new YtDlpException($"yt-dlp preview failed.{DescribeError(result)}");

        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;

        var metadata = YtDlpJsonMapper.MapMetadata(document);
        var durationSeconds = YtDlpJsonMapper.GetDurationSeconds(root);

        var sizes = PredictSizes(root, durationSeconds);
        var qualities = VideoQualitySelector.GetFitting(
            _options.MaxUploadBytes,
            _options.VideoQualities,
            sizes);

        return new VideoPreview(metadata, qualities);
    }

    private IReadOnlyDictionary<int, long?> PredictSizes(JsonElement root, int? durationSeconds)
    {
        var result = new Dictionary<int, long?>();
        if (!root.TryGetProperty("formats", out var formatsElement)
            || formatsElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var formats = formatsElement.EnumerateArray().ToList();
        var bestAudio = PickBestAudio(formats);
        var audioSize = bestAudio is null ? null : EstimateSize(bestAudio.Value, durationSeconds);

        foreach (var height in _options.VideoQualities)
        {
            var video = PickBestVideo(formats, height);
            if (video is null) continue;

            var videoSize = EstimateSize(video.Value, durationSeconds);
            result[height] = videoSize is null || audioSize is null
                ? null
                : videoSize + audioSize;
        }

        return result;
    }

    private static JsonElement? PickBestVideo(List<JsonElement> formats, int maxHeight)
    {
        var candidates = formats
            .Where(f => IsVideoOnly(f) && TryGetHeight(f, out var h) && h <= maxHeight)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = formats
                .Where(f => IsCombined(f) && TryGetHeight(f, out var h) && h <= maxHeight)
                .ToList();
        }

        if (candidates.Count == 0) return null;

        return candidates
            .OrderByDescending(GetHeight)
            .ThenBy(GetCodecPriority)
            .ThenByDescending(GetTbr)
            .First();
    }

    private static JsonElement? PickBestAudio(List<JsonElement> formats)
    {
        var candidates = formats.Where(IsAudioOnly).ToList();
        if (candidates.Count == 0) return null;

        return candidates
            .OrderByDescending(GetTbr)
            .First();
    }

    private static long? EstimateSize(JsonElement format, int? durationSeconds)
    {
        if (TryGetLong(format, "filesize", out var filesize) && filesize > 0) return filesize;
        if (TryGetLong(format, "filesize_approx", out var approx) && approx > 0) return approx;

        var tbr = GetTbr(format);
        if (tbr > 0 && durationSeconds is > 0)
        {
            return (long)(tbr / 8.0 * 1000 * durationSeconds.Value);
        }

        return null;
    }

    private static bool IsVideoOnly(JsonElement format)
    {
        var vcodec = GetString(format, "vcodec");
        var acodec = GetString(format, "acodec");
        return !string.IsNullOrWhiteSpace(vcodec) && vcodec != "none"
               && (string.IsNullOrWhiteSpace(acodec) || acodec == "none");
    }

    private static bool IsAudioOnly(JsonElement format)
    {
        var vcodec = GetString(format, "vcodec");
        var acodec = GetString(format, "acodec");
        return (string.IsNullOrWhiteSpace(vcodec) || vcodec == "none")
               && !string.IsNullOrWhiteSpace(acodec) && acodec != "none";
    }

    private static bool IsCombined(JsonElement format)
    {
        var vcodec = GetString(format, "vcodec");
        var acodec = GetString(format, "acodec");
        return !string.IsNullOrWhiteSpace(vcodec) && vcodec != "none"
               && !string.IsNullOrWhiteSpace(acodec) && acodec != "none";
    }

    private static int GetCodecPriority(JsonElement format)
    {
        var vcodec = GetString(format, "vcodec") ?? string.Empty;
        if (vcodec.Contains("av01", StringComparison.OrdinalIgnoreCase)) return 0;
        if (vcodec.Contains("vp9", StringComparison.OrdinalIgnoreCase)) return 1;
        if (vcodec.Contains("h264", StringComparison.OrdinalIgnoreCase)
            || vcodec.Contains("avc", StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }

    private static bool TryGetHeight(JsonElement format, out int height)
    {
        height = 0;
        return format.TryGetProperty("height", out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt32(out height);
    }

    private static int GetHeight(JsonElement format)
    {
        TryGetHeight(format, out var height);
        return height;
    }

    private static double GetTbr(JsonElement format)
    {
        if (format.TryGetProperty("tbr", out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var tbr))
        {
            return tbr;
        }

        return 0;
    }

    private static bool TryGetLong(JsonElement format, string name, out long value)
    {
        value = 0;
        return format.TryGetProperty(name, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetInt64(out value);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<(string Path, int? Width, int? Height)> DownloadAsync(
        string url, string filePath, int maxHeight, CancellationToken cancellationToken)
    {
        var downloadFormat = $"-f \"bv*[height<={maxHeight}]+ba/b\" --merge-output-format mp4";

        var result = await _runner.RunAsync(
            $"{GetCookieArg()} {downloadFormat} --no-progress --verbose " +
            $"-o \"{filePath}.%(ext)s\" " +
            $"--print after_move:filepath " +
            $"--print after_move:\"%(width)s\\t%(height)s\\t%(ext)s\" \"{url}\"",
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new YtDlpException($"yt-dlp download failed (Exit {result.ExitCode}).{DescribeError(result)}");
        }

        var lines = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .ToArray();

        var actualPath = filePath;
        int? width = null;
        int? height = null;

        if (lines.Length >= 2 && !string.IsNullOrWhiteSpace(lines[^2]))
        {
            actualPath = lines[^2];
        }

        if (lines.Length >= 1)
        {
            var parts = lines[^1].Split("\\t");
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[0], out var w)) width = w;
                if (int.TryParse(parts[1], out var h)) height = h;
            }
        }

        if (!File.Exists(actualPath)) throw new YtDlpException($"Download completed but file not found: {actualPath}");

        return (actualPath, width, height);
    }

    private string GetUniqueTempPath() =>
        Path.Combine(_storagePath, Guid.NewGuid().ToString("N"));

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temporary file {Path}", path);
        }
    }

    private string GetCookieArg() =>
        string.IsNullOrWhiteSpace(_options.CookieFromBrowser)
            ? string.Empty
            : $"--cookies-from-browser {_options.CookieFromBrowser}";

    private static string DescribeError(YtDlpResult result) =>
        string.IsNullOrEmpty(result.Error) ? string.Empty : $"\nERROR: {result.Error}";
}
