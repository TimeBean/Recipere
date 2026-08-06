using System.Net;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Recipere.Application.Get;
using Recipere.Application.GetMetadata;
using Recipere.Application.Remove;
using Recipere.Core.Model;
using Recipere.Core.Repository;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Recipere.Presentation.Telegram;

public sealed class MessageHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ISender _mediator;
    private readonly IVideoStorage _videoStorage;
    private readonly ILogger<MessageHandler> _logger;
    private readonly MessageOptions _options;

    public MessageHandler(
        ITelegramBotClient botClient,
        ISender mediator,
        IVideoStorage videoStorage,
        ILogger<MessageHandler> logger,
        IOptions<MessageOptions> options)
    {
        _botClient = botClient;
        _mediator = mediator;
        _videoStorage = videoStorage;
        _logger = logger;
        _options = options.Value;
    }

    public async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            return;
        }

        _logger.LogInformation("Received '{Text}' in {Chat}", message.Text, message.Chat);

        if (message.Text.StartsWith("/start", StringComparison.Ordinal))
        {
            await _botClient.SendMessage(
                message.Chat,
                _options.HelpText,
                cancellationToken: cancellationToken);
            return;
        }

        if (!UrlExtractor.TryExtract(message.Text, out var url))
        {
            await _botClient.SendMessage(
                message.Chat,
                _options.MissingUrlText,
                cancellationToken: cancellationToken);
            return;
        }

        await ProcessAsync(url, message, cancellationToken);
    }

    private async Task ProcessAsync(string url, Message message, CancellationToken cancellationToken)
    {
        Content? content = null;
        try
        {
            _logger.LogInformation("Fetching metadata for {Url}", url);

            var metadata = await _mediator.Send(new GetMetadataRequest(url), cancellationToken);
            var infoMessage = await _botClient.SendMessage(
                message.Chat,
                string.Format(
                    _options.DownloadingTemplate,
                    EscapeHtml(metadata.Title.Value)),
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            await TryDeleteMessageAsync(message, cancellationToken);

            _logger.LogInformation("Downloading audio from {Url}", url);

            content = await _mediator.Send(new GetRequest(url), cancellationToken);

            await using var stream = await _videoStorage.OpenAsync(content.Path, cancellationToken);
            await _botClient.SendAudio(
                message.Chat,
                InputFile.FromStream(stream, GetAudioFileName(content.Title.Value)),
                title: content.Title.Value,
                performer: content.Channel.Name.Value,
                duration: DurationParser.ParseSeconds(content.DurationString.Value),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Sent audio for {Url}, removing from storage", url);
            await _mediator.Send(new RemoveRequest(content.Path), cancellationToken);

            await TryDeleteMessageAsync(infoMessage, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing of {Url} was cancelled", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {Url}", url);
            await TryCleanupAsync(url, content, cancellationToken);
            await SendFailureAsync(message.Chat, ErrorTextResolver.Resolve(ex, _options), cancellationToken);
        }
    }

    private async Task TryDeleteMessageAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.DeleteMessage(message.Chat, message.MessageId, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete message in {Chat}", message.Chat);
        }
    }

    private async Task TryCleanupAsync(string url, Content? content, CancellationToken cancellationToken)
    {
        if (content is null || string.IsNullOrEmpty(content.Path))
        {
            return;
        }

        try
        {
            await _mediator.Send(new RemoveRequest(content.Path), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up {Path} after processing {Url} failed", content.Path, url);
        }
    }

    private async Task SendFailureAsync(ChatId chatId, string text, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error message to {Chat}", chatId);
        }
    }

    private static string EscapeHtml(string value) => WebUtility.HtmlEncode(value);

    private static string GetAudioFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Concat(title.Select(c => invalid.Contains(c) ? '_' : c));
        return $"{name}.mp3";
    }
}
