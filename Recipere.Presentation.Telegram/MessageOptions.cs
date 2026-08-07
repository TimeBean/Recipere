namespace Recipere.Presentation.Telegram;

public sealed class MessageOptions
{
    public required string StartText { get; init; }
    public required string HelpText { get; init; }
    public required string MissingUrlText { get; init; }
    public required string FailureText { get; init; }
    public required string DownloadingTemplate { get; init; }

    public string ChoosingTemplate { get; init; } = string.Empty;
    public string DownloadingAudioTemplate { get; init; } = string.Empty;
    public string DownloadingVideoTemplate { get; init; } = string.Empty;
    public string VideoTooLongText { get; init; } = string.Empty;
    public string VideoCaptionTemplate { get; init; } = string.Empty;
    public string AudioCaptionTemplate { get; init; } = string.Empty;
    public string ExpiredRequestText { get; init; } = string.Empty;

    public ErrorCause[] ErrorCauses { get; init; } = [];
}

public sealed class ErrorCause
{
    public string[] Contains { get; init; } = [];
    public string? Response { get; init; }
}
