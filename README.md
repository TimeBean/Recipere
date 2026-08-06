# Recipere

Telegram bot for downloading video.

## Architecture

- `Recipere.Core` — domain models (`Content`, `Channel`, `Text`), the `IContentRepository` contract and the `IVideoStorage` storage contract.
- `Recipere.Application` — MediatR requests and handlers (`Get`, `GetMetadata`, `Remove`).
- `Recipere.Infrastructure` — `YtDlpContentRepository` (a configurable yt-dlp-based implementation) and `InMemoryVideoStorage` (in-memory video storage).
- `Recipere.Presentation.Telegram` — Telegram bot host (`Host` + `IHostedService`), message handler and helpers.
- `Recipere.Presentation.Console` — simple sample that downloads a hardcoded URL.

## Setup (Telegram bot)

The bot token is not stored in the repository. In development it lives in .NET user secrets.

1. Add the `<UserSecretsId>` already present in `Recipere.Presentation.Telegram.csproj`:

   ```bash
   dotnet user-secrets set "TelegramBot:Token" "YOUR_BOT_TOKEN_HERE" \
     --project Recipere.Presentation.Telegram
   ```

2. Optionally copy `appsettings.Example.json` to `appsettings.json` and tune yt-dlp settings
   (`YtDlp:CookieFromBrowser`, `YtDlp:AudioOnly`, `YtDlp:MaxUploadBytes`).
   `appsettings.json` is gitignored so machine-local values never get committed.

3. Run in development (user secrets are only loaded when the environment is `Development`):

   ```bash
   DOTNET_ENVIRONMENT=Development dotnet run --project Recipere.Presentation.Telegram
   ```

In production, set the token via the `TelegramBot__Token` environment variable instead.

## Prerequisites

- .NET 10 SDK
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) on `PATH`
