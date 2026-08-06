using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Recipere.Core.Model;
using Recipere.Core.Repository;

namespace Recipere.Infrastructure.Repository;

public sealed class YtDlpContentRepository : IContentRepository
{
    private const string YtDlp = "yt-dlp";
    private const long MaxAudioBytes = 45L * 1024 * 1024;

    private const string MetadataPrintTemplate =
        "%(title)s\\t%(webpage_url)s\\t%(thumbnail)s\\t%(channel)s\\t%(channel_url)s\\t%(duration_string)s";

    private static string DefaultStoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Recipere",
        "downloads");

    private readonly ILogger<YtDlpContentRepository> _logger;
    private readonly YtDlpOptions _options;
    private readonly IVideoStorage _videoStorage;
    private readonly string _storagePath;

    public YtDlpContentRepository(
        IOptions<YtDlpOptions> options,
        IVideoStorage videoStorage,
        ILogger<YtDlpContentRepository> logger)
    {
        _options = options.Value;
        _videoStorage = videoStorage;
        _logger = logger;
        _storagePath = string.IsNullOrWhiteSpace(_options.StoragePath)
            ? DefaultStoragePath
            : _options.StoragePath;
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<Content> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(url, cancellationToken);

        var audioQuality = _options.AudioOnly
            ? GetAudioQuality(ParseDurationSeconds(metadata.DurationString.Value))
            : 0;

        var actualPath = await DownloadAsync(url, GetUniqueTempPath(), audioQuality, cancellationToken);

        if (actualPath.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
            || File.Exists(actualPath + ".part"))
        {
            TryDeleteFile(actualPath);
            throw new YtDlpException($"Download resulted in .part file: {actualPath}");
        }

        try
        {
            metadata.Path = await _videoStorage.SaveAsync(actualPath, cancellationToken);
        }
        finally
        {
            TryDeleteFile(actualPath);
        }

        return metadata;
    }

    public Task RemoveAsync(string handle, CancellationToken cancellationToken = default) =>
        _videoStorage.DeleteAsync(handle, cancellationToken);

    public async Task<bool> ExistAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = await RunYtDlpAsync($"--skip-download --print title \"{url}\"", cancellationToken);
        return result.ExitCode == 0;
    }

    public Task<bool> HasAsync(string handle, CancellationToken cancellationToken = default) =>
        _videoStorage.ContainsAsync(handle, cancellationToken);

    public async Task<Content> GetMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = await RunYtDlpAsync(
            $"{GetCookieArg()} --skip-download --print \"{MetadataPrintTemplate}\" \"{url}\"",
            cancellationToken);

        if (result.ExitCode != 0) throw new YtDlpException($"yt-dlp metadata fetch failed.{DescribeError(result)}");

        var parts = result.Output.Split("\\t");
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

    private async Task<string> DownloadAsync(string url, string filePath, int audioQuality,
        CancellationToken cancellationToken)
    {
        var downloadFormat = _options.AudioOnly
            ? $"-f ba -x --audio-format mp3 --audio-quality {audioQuality}"
            : "-f \"bv*+ba/b\" --merge-output-format mp4";

        var result = await RunYtDlpAsync(
            $"{GetCookieArg()} {downloadFormat} --no-progress --verbose " +
            $"-o \"{filePath}.%(ext)s\" --print after_move:filepath \"{url}\"",
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new YtDlpException($"yt-dlp download failed (Exit {result.ExitCode}).{DescribeError(result)}");
        }

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var actualPath = lines.Length > 0 ? lines[^1].Trim() : filePath;

        if (!File.Exists(actualPath)) throw new YtDlpException($"Download completed but file not found: {actualPath}");

        return actualPath;
    }

    private async Task<YtDlpResult> RunYtDlpAsync(string arguments, CancellationToken cancellationToken)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = YtDlp,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.OutputDataReceived += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.Data)) return;
            outputBuilder.AppendLine(args.Data);
            _logger.LogDebug("[yt-dlp out] {Line}", args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.Data)) return;
            errorBuilder.AppendLine(args.Data);
            _logger.LogTrace("[yt-dlp err] {Line}", args.Data);
        };

        if (!process.Start()) throw new YtDlpException("Failed to start yt-dlp process.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new YtDlpResult(
            process.ExitCode,
            outputBuilder.ToString().Trim(),
            errorBuilder.ToString().Trim());
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

    private static int GetAudioQuality(int? durationSeconds)
    {
        if (durationSeconds is null or <= 0)
        {
            return 3;
        }

        var maxKbps = MaxAudioBytes / durationSeconds.Value / 128;
        int[] qualityKbps = { 245, 225, 190, 175, 165, 130, 115, 100, 85, 65 };

        for (var quality = 0; quality < qualityKbps.Length; quality++)
            if (maxKbps >= qualityKbps[quality])
            {
                return quality;
            }

        return 9;
    }

    private static int? ParseDurationSeconds(string durationString)
    {
        if (string.IsNullOrWhiteSpace(durationString)) return null;

        var parts = durationString.Split(':');
        if (parts.Length is 0 or > 3) return null;

        var total = 0;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var value)) return null;
            total = total * 60 + value;
        }

        return total;
    }

    private sealed record YtDlpResult(int ExitCode, string Output, string Error);
}