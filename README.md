# Recipere

Telegram bot for downloading video.

## Prerequisites

- .NET 10 SDK
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) on `PATH`

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

