# Recipere

Telegram bot for downloading video.

## Prerequisites

- .NET 10 SDK
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) on `PATH`

## Setup (Telegram bot)

```bash
dotnet user-secrets set "TelegramBot:Token" "YOUR_BOT_TOKEN_HERE" \
  --project Recipere.Presentation.Telegram
DOTNET_ENVIRONMENT=Development dotnet run --project Recipere.Presentation.Telegram
```

In production, set the token via the `TelegramBot__Token` environment variable instead.
Optionally copy `appsettings.Example.json` to `appsettings.json` (gitignored) and tune
settings (see [Configuration](#configuration)).

## Configuration

All settings live in `appsettings.json` (or `appsettings.Development.json` in development).
A ready-made template is in `appsettings.Example.json`. Any key can be overridden with an
environment variable by replacing `:` with `__` (e.g. `YtDlp__MaxUploadBytes`).

### `TelegramBot`

| Key            | Type   | Description                                  |
| -------------- | ------ | -------------------------------------------- |
| `Token`        | string | Telegram bot token from [@BotFather](https://t.me/BotFather). |

### `Messages`

All bot reply texts. Telegram supports basic HTML formatting (`<b>`, `<i>`, `<code>`);
the `{0}` placeholder is replaced with the user's link.

| Key                   | Type     | Description                                                   |
| --------------------- | -------- | ------------------------------------------------------------- |
| `StartText`           | string   | Reply to `/start`.                                             |
| `HelpText`            | string   | Reply to `/help`.                                              |
| `MissingUrlText`      | string   | Reply when the message contains no link.                       |
| `FailureText`         | string   | Reply when the download fails and no `ErrorCauses` rule matches. |
| `DownloadingTemplate` | string   | "Downloading…" message; `{0}` is replaced with the link.       |
| `ErrorCauses`         | array    | Ordered list of error-to-reply mappings (see below).           |

Each `ErrorCauses` item maps yt-dlp error keywords to a user-friendly reply:

| Key        | Type     | Description                                                        |
| ---------- | -------- | ------------------------------------------------------------------ |
| `Contains` | string[] | Keywords matched against the yt-dlp error message (case-insensitive). |
| `Response` | string?  | Reply shown when a keyword matches. Empty/null falls back to `FailureText`. |

Rules are checked in order; the first match wins.

### `YtDlp`

| Key                 | Type    | Default                                    | Description                                                                   |
| ------------------- | ------- | ------------------------------------------ | ----------------------------------------------------------------------------- |
| `StoragePath`       | string  | `%LocalAppData%/Recipere/downloads`        | Where downloaded files are stored.                                             |
| `AudioOnly`         | bool    | `true`                                     | Extract only the audio track (MP3) instead of the full video.                  |
| `CookieFromBrowser` | string? | empty                                      | Browser name (`firefox`, `chrome`, …) used to bypass login/age restrictions.   |
| `MaxUploadBytes`    | integer | `52428800` (50 MB)                         | Maximum file size uploaded to Telegram; larger files are split.                |

See [yt-dlp filesystem options](https://github.com/yt-dlp/yt-dlp/blob/5d6b8c8cd19785c3086ae3a9ec618c45e25eb3bc/README.md#filesystem-options) for available browser names, and [Supported sites](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md) for the list of supported platforms.


