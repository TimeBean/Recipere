# Recipere

Telegram bot for downloading audio and video.

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

All bot reply texts. Telegram supports basic HTML formatting (`<b>`, `<i>`, `<code>`).
Placeholders are replaced at runtime: `{0}` is the media title, `{1}` (in `ChoosingTemplate`)
is the duration.

| Key                     | Type     | Description                                                   |
| ----------------------- | -------- | ------------------------------------------------------------- |
| `StartText`             | string   | Reply to `/start`.                                             |
| `HelpText`              | string   | Reply to `/help`.                                              |
| `MissingUrlText`        | string   | Reply when the message contains no link.                       |
| `FailureText`           | string   | Reply when the download fails and no `ErrorCauses` rule matches. |
| `DownloadingTemplate`   | string   | "Downloading…" message; `{0}` is the title. Used as a fallback for the two templates below. |
| `ChoosingTemplate`      | string?  | Format-selection card shown after a link is sent; `{0}` = title, `{1}` = duration. Defaults to a built-in text if empty. |
| `DownloadingAudioTemplate` | string? | "Downloading audio…" message; falls back to `DownloadingTemplate`. |
| `DownloadingVideoTemplate` | string? | "Downloading video…" message; falls back to `DownloadingTemplate`. |
| `VideoTooLongText`      | string?  | Shown on the selection card when no video quality fits the size limit. |
| `VideoCaptionTemplate`  | string?  | Caption attached to the sent video; `{0}` is the title, `{1}` is the channel name linked to its page, `{2}` is the bot's @username link (all optional, missing values render as empty). No caption is sent when empty. |
| `AudioCaptionTemplate`  | string?  | Caption attached to the sent audio; same placeholders as `VideoCaptionTemplate`. No caption is sent when empty. |
| `ExpiredRequestText`    | string?  | Reply when the user taps an expired format button.            |
| `ErrorCauses`           | array    | Ordered list of error-to-reply mappings (see below).           |

## How a download works

1. The user sends a link.
2. The bot fetches metadata and replies with a card showing the title, duration and an
   inline keyboard: `Audio` plus one button per video quality.
3. Only qualities whose estimated size fits `YtDlp:MaxUploadBytes` are shown; if none fit,
   only `Audio` is offered.
4. Tapping a button downloads and sends the file (MP3 via `sendAudio`, MP4 via `sendVideo`).

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
| `AudioOnly`         | bool    | `true`                                     | Default format used by the audio pipeline (not the Telegram selection flow).   |
| `VideoQualities`    | int[]   | `[360, 480, 720, 1080]`                    | Video qualities offered in the selection card. Only those fitting the size limit are shown. |
| `CookieFromBrowser` | string? | empty                                      | Browser used to bypass login/age restrictions. See the note below.             |
| `MaxUploadBytes`    | integer | `52428800` (50 MB)                         | Video size budget. Qualities whose estimated size exceeds it are hidden; downloaded videos larger than it are rejected. |

See [yt-dlp filesystem options](https://github.com/yt-dlp/yt-dlp/blob/5d6b8c8cd19785c3086ae3a9ec618c45e25eb3bc/README.md#filesystem-options) for available browser names, and [Supported sites](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md) for the list of supported platforms.

> **Note:** `CookieFromBrowser` maps directly to yt-dlp's
> `--cookies-from-browser BROWSER[+KEYRING][:PROFILE][::CONTAINER]`, so the full
> syntax is supported (e.g. `firefox`, `chrome+system`, `edge::Default`, …).


