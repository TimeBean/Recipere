using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Recipere.Infrastructure.Repository;

public sealed class YtDlpProcessRunner
{
    private const string YtDlp = "yt-dlp";

    private readonly ILogger _logger;

    public YtDlpProcessRunner(ILogger logger) => _logger = logger;

    public async Task<YtDlpResult> RunAsync(string arguments, CancellationToken cancellationToken)
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
}

public sealed record YtDlpResult(int ExitCode, string Output, string Error);
