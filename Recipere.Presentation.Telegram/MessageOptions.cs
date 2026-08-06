namespace Recipere.Presentation.Telegram;

public sealed class MessageOptions
{
    public required string HelpText { get; init; }
    public required string MissingUrlText { get; init; }
    public required string FailureText { get; init; }
    public required string DownloadingTemplate { get; init; }

    public ErrorCause[] ErrorCauses { get; init; } = [];
}

public sealed class ErrorCause
{
    public string[] Contains { get; init; } = [];
    public string? Response { get; init; }
}
