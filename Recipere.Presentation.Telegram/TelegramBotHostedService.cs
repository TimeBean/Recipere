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
        _botClient.OnMessage += OnMessageAsync;
        _botClient.OnError += OnErrorAsync;

        var me = await _botClient.GetMe(cancellationToken);
        _logger.LogInformation("@{Username} is running...", me.Username);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _botClient.OnMessage -= OnMessageAsync;
        _botClient.OnError -= OnErrorAsync;
        _processingCts.Cancel();
        return Task.CompletedTask;
    }

    private Task OnMessageAsync(Message message, UpdateType type)
        => _messageHandler.HandleMessageAsync(message, _processingCts.Token);

    private Task OnErrorAsync(Exception exception, HandleErrorSource source)
    {
        _logger.LogError(exception, "Telegram polling error (source: {Source})", source);
        return Task.CompletedTask;
    }
}
