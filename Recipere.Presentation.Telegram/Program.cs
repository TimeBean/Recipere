using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Recipere.Application.Get;
using Recipere.Core.Repository;
using Recipere.Infrastructure.Repository;
using Telegram.Bot;

namespace Recipere.Presentation.Telegram;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Services.AddHostedService<TelegramBotHostedService>();
        builder.Services.AddSingleton<MessageHandler>();
        builder.Services.Configure<TelegramBotOptions>(builder.Configuration.GetSection("TelegramBot"));
        builder.Services.Configure<YtDlpOptions>(builder.Configuration.GetSection("YtDlp"));
        builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(GetRequest).Assembly));
        builder.Services.AddSingleton<IContentRepository, YtDlpContentRepository>();
        builder.Services.AddSingleton<IVideoStorage, InMemoryVideoStorage>();
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.Token))
                throw new InvalidOperationException(
                    "Telegram bot token is not configured. Set 'TelegramBot:Token' (e.g. via dotnet user-secrets).");
            return new TelegramBotClient(options.Token);
        });
        builder.Services.AddSingleton<ITelegramBotClient>(sp => sp.GetRequiredService<TelegramBotClient>());

        await builder.Build().RunAsync();
    }
}
