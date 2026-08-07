using System.Net;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Recipere.Application.Get;
using Recipere.Application.GetVideo;
using Recipere.Application.Remove;
using Recipere.Core.Model;
using Recipere.Core.Repository;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VideoQuality = Recipere.Core.Model.VideoQuality;

namespace Recipere.Presentation.Telegram;

public sealed class MessageHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ISender _mediator;
    private readonly IVideoStorage _videoStorage;
    private readonly PendingRequestStore _pendingRequests;
    private readonly ILogger<MessageHandler> _logger;
    private readonly MessageOptions _options;

    public MessageHandler(
        ITelegramBotClient botClient,
        ISender mediator,
        IVideoStorage videoStorage,
        PendingRequestStore pendingRequests,
        ILogger<MessageHandler> logger,
        IOptions<MessageOptions> options)
    {
        _botClient = botClient;
        _mediator = mediator;
        _videoStorage = videoStorage;
        _pendingRequests = pendingRequests;
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
            await SendTextAsync(message.Chat, _options.StartText, cancellationToken);
            return;
        }

        if (message.Text.StartsWith("/help", StringComparison.Ordinal))
        {
            await SendTextAsync(message.Chat, _options.HelpText, cancellationToken);
            return;
        }

        if (!UrlExtractor.TryExtract(message.Text, out var url))
        {
            await _botClient.SendMessage(
                message.Chat,
                _options.MissingUrlText,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            return;
        }

        await SendSelectionCardAsync(url, message, cancellationToken);
    }

    public async Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

            if (callbackQuery.Message is null) return;

            var (isAudio, height, token) = ParseCallbackData(callbackQuery.Data);
            if (string.IsNullOrEmpty(token) || !_pendingRequests.TryGet(token, out var pending))
            {
                var expiredText = string.IsNullOrWhiteSpace(_options.ExpiredRequestText)
                    ? "This request has expired. Please send the link again."
                    : _options.ExpiredRequestText;
                await SendTextAsync(callbackQuery.Message.Chat, expiredText, cancellationToken);
                await TryDeleteMessageAsync(callbackQuery.Message, cancellationToken);
                return;
            }

            var downloadingText = string.Format(
                ResolveDownloadingTemplate(isAudio),
                EscapeHtml(pending.Content.Title.Value));
            var downloadingMessage = await _botClient.SendMessage(
                callbackQuery.Message.Chat,
                downloadingText,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            await TryDeleteMessageAsync(callbackQuery.Message, cancellationToken);

            Content? content = null;
            try
            {
                if (isAudio)
                {
                    _logger.LogInformation("Downloading audio from {Url}", pending.Url);
                    content = await _mediator.Send(new GetRequest(pending.Url), cancellationToken);

                    await using var stream = await _videoStorage.OpenAsync(content.Path, cancellationToken);
                    await _botClient.SendAudio(
                        callbackQuery.Message.Chat,
                        InputFile.FromStream(stream, GetAudioFileName(content.Title.Value)),
                        title: content.Title.Value,
                        performer: content.Channel.Name.Value,
                        duration: DurationParser.ParseSeconds(content.DurationString.Value),
                        caption: await BuildCaptionAsync(content, _options.AudioCaptionTemplate, cancellationToken),
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Downloading video from {Url} (max {Height}p)", pending.Url, height);
                    content = await _mediator.Send(new GetVideoRequest(pending.Url, height!.Value), cancellationToken);

                    await using var stream = await _videoStorage.OpenAsync(content.Path, cancellationToken);
                    await _botClient.SendVideo(
                        callbackQuery.Message.Chat,
                        InputFile.FromStream(stream, GetVideoFileName(content)),
                        width: content.Width,
                        height: content.Height,
                        duration: DurationParser.ParseSeconds(content.DurationString.Value),
                        caption: await BuildCaptionAsync(content, _options.VideoCaptionTemplate, cancellationToken),
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                }

                _logger.LogInformation("Sent media for {Url}, removing from storage", pending.Url);
                await _mediator.Send(new RemoveRequest(content.Path), cancellationToken);

                await TryDeleteMessageAsync(downloadingMessage, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Processing of {Url} was cancelled", pending.Url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {Url}", pending.Url);
                await TryCleanupAsync(pending.Url, content, cancellationToken);
                await SendFailureAsync(callbackQuery.Message.Chat, ErrorTextResolver.Resolve(ex, _options), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle callback {Data}", callbackQuery.Data);
        }
    }

    private async Task SendSelectionCardAsync(string url, Message message, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching metadata for {Url}", url);

            var preview = await _mediator.Send(new GetVideoPreviewRequest(url), cancellationToken);

            var token = _pendingRequests.Add(url, preview.Metadata);

            await _botClient.SendMessage(
                message.Chat,
                BuildCardText(preview.Metadata, videoUnavailable: preview.Qualities.Count == 0),
                parseMode: ParseMode.Html,
                replyMarkup: BuildSelectionKeyboard(preview.Qualities, token),
                cancellationToken: cancellationToken);

            await TryDeleteMessageAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing of {Url} was cancelled", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare {Url}", url);
            await SendFailureAsync(message.Chat, ErrorTextResolver.Resolve(ex, _options), cancellationToken);
        }
    }

    private InlineKeyboardMarkup BuildSelectionKeyboard(IReadOnlyList<VideoQuality> qualities, string token)
    {
        var rows = new List<IEnumerable<InlineKeyboardButton>>
        {
            new[] { InlineKeyboardButton.WithCallbackData("🎵 Audio", $"a:{token}") }
        };

        if (qualities.Count > 0)
        {
            rows.Add(qualities.Select(q => InlineKeyboardButton.WithCallbackData(q.Label, $"v:{q.Height}:{token}")));
        }

        return new InlineKeyboardMarkup(rows);
    }

    private string BuildCardText(Content metadata, bool videoUnavailable)
    {
        var title = EscapeHtml(metadata.Title.Value);
        var duration = EscapeHtml(metadata.DurationString.Value);

        var text = string.IsNullOrWhiteSpace(_options.ChoosingTemplate)
            ? $"🎬 <b>{title}</b>\n⏱ {duration}\n\nChoose a format:"
            : string.Format(_options.ChoosingTemplate, title, duration);

        if (videoUnavailable && !string.IsNullOrWhiteSpace(_options.VideoTooLongText))
        {
            text += "\n\n" + _options.VideoTooLongText;
        }

        return text;
    }

    private string ResolveDownloadingTemplate(bool isAudio)
    {
        var template = isAudio ? _options.DownloadingAudioTemplate : _options.DownloadingVideoTemplate;
        return string.IsNullOrWhiteSpace(template) ? _options.DownloadingTemplate : template;
    }

    private async Task<string?> BuildCaptionAsync(Content content, string? template, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }
        var title = EscapeHtml(content.Title.Value);
        var channelName = EscapeHtml(content.Channel.Name.Value);
        var channel = string.IsNullOrWhiteSpace(content.Channel.Url)
            ? channelName
            : $"<a href=\"{WebUtility.HtmlEncode(content.Channel.Url)}\">{channelName}</a>";
        var botMention = await GetBotMentionAsync(cancellationToken);
        return string.Format(template, title, channel, botMention);
    }

    private string? _botMention;

    private async Task<string> GetBotMentionAsync(CancellationToken cancellationToken)
    {
        if (_botMention is not null)
        {
            return _botMention;
        }

        var me = await _botClient.GetMe(cancellationToken);
        _botMention = string.IsNullOrWhiteSpace(me.Username)
            ? string.Empty
            : $"<a href=\"tg://resolve?domain={WebUtility.HtmlEncode(me.Username)}\">@{WebUtility.HtmlEncode(me.Username)}</a>";
        return _botMention;
    }

    private static (bool IsAudio, int? Height, string Token) ParseCallbackData(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return (false, null, string.Empty);

        var parts = data.Split(':');
        if (parts.Length < 2) return (false, null, string.Empty);

        var token = parts[^1];
        if (parts[0] == "a") return (true, null, token);
        if (parts[0] == "v" && parts.Length >= 3 && int.TryParse(parts[1], out var height))
        {
            return (false, height, token);
        }

        return (false, null, string.Empty);
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

    private async Task SendTextAsync(ChatId chatId, string text, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(chatId, text, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
    }

    private async Task SendFailureAsync(ChatId chatId, string text, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.SendMessage(chatId, text, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
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

    private static string GetVideoFileName(Content content)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Concat(content.Title.Value.Select(c => invalid.Contains(c) ? '_' : c));
        var ext = string.IsNullOrWhiteSpace(content.Extension) ? "mp4" : content.Extension;
        return $"{name}.{ext}";
    }
}
