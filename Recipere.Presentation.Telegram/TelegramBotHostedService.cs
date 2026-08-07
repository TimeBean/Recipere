using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Recipere.Presentation.Telegram;

public sealed class TelegramBotHostedService : IHostedService
{
    private readonly TelegramBotClient _botClient;
    private readonly MessageHandler _messageHandler;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly CancellationTokenSource _processingCts = new();

    public TelegramBotHostedService(
        TelegramBotClient botClient,
        MessageHandler messageHandler,
        ILogger<TelegramBotHostedService> logger)
    {
        _botClient = botClient;
        _messageHandler = messageHandler;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _botClient.OnUpdate += OnUpdateAsync;
        _botClient.OnMessage += OnMessageAsync;
        _botClient.OnError += OnErrorAsync;

        var me = await _botClient.GetMe(cancellationToken);
        _logger.LogInformation("@{Username} is running...", me.Username);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _botClient.OnUpdate -= OnUpdateAsync;
        _botClient.OnMessage -= OnMessageAsync;
        _botClient.OnError -= OnErrorAsync;
        _processingCts.Cancel();
        return Task.CompletedTask;
    }

    private Task OnUpdateAsync(Update update)
    {
        if (update.CallbackQuery is null)
        {
            return Task.CompletedTask;
        }

        _ = HandleAsync();
        return Task.CompletedTask;

        async Task HandleAsync()
        {
            try
            {
                await _messageHandler.HandleCallbackAsync(update.CallbackQuery, _processingCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing callback in {Chat}", update.CallbackQuery.Message?.Chat);
            }
        }
    }

    private Task OnMessageAsync(Message message, UpdateType type)
    {
        _ = HandleAsync();
        return Task.CompletedTask;

        async Task HandleAsync()
        {
            try
            {
                await _messageHandler.HandleMessageAsync(message, _processingCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing message in {Chat}", message.Chat);
            }
        }
    }

    private Task OnErrorAsync(Exception exception, HandleErrorSource source)
    {
        _logger.LogError(exception, "Telegram polling error (source: {Source})", source);
        return Task.CompletedTask;
    }
}
