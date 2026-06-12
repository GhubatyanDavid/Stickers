# SoundSticker

ASP.NET Core backend for the sound sticker MVP.

## What works now

- Upload local media files.
- Return preview metadata for timeline controls after upload.
- Store uploaded media under `storage/originals`.
- Store media and sticker metadata in PostgreSQL.
- Create a moving sticker job from an uploaded video or GIF.
- Create an image sticker job from an uploaded image by looping it into MP4, GIF, or WebP.
- Export original-aspect, square, circle, portrait, and landscape stickers.
- Remove simple solid-color backgrounds from image and GIF sources.
- Keep original video audio, mute it, or use audio from another uploaded audio/video file.
- Trim video and audio tracks from different time ranges.
- Process sticker jobs in a background worker.
- Save generated MP4/GIF/WebP stickers under `storage/stickers`.
- Mark stickers as private or public during creation.
- Delete old or failed sticker jobs and their generated MP4/GIF/WebP files.
- Serve local media files from `/media`.

## Run locally

Configure PostgreSQL first:

```json
{
  "Persistence": {
    "Provider": "PostgreSql",
    "ConnectionStringName": "Postgres",
    "AutoCreateSchema": true
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=soundsticker;Username=appuser;Password=change-me"
  }
}
```

For production, prefer environment variables or a systemd override instead of
committing the real password:

```ini
[Service]
Environment="ConnectionStrings__Postgres=Host=127.0.0.1;Port=5432;Database=soundsticker;Username=appuser;Password=your-real-password"
```

The API creates the `media_files` and `stickers` tables on startup when
`AutoCreateSchema` is true.

```powershell
dotnet run --project SoundSticker/SoundSticker.csproj --launch-profile http
```

Open:

```text
http://localhost:5258/swagger
http://localhost:5258/api/health
```

## Important

Video trimming needs FFmpeg. Timeline preview metadata uses FFprobe. Install
both tools and make sure `ffmpeg` and `ffprobe` are available in `PATH`, or set:

```json
{
  "Ffmpeg": {
    "ExecutablePath": "C:\\path\\to\\ffmpeg.exe",
    "ProbeExecutablePath": "C:\\path\\to\\ffprobe.exe",
    "CwebpExecutablePath": "C:\\path\\to\\cwebp.exe"
  }
}
```

WebP output also needs Google's WebP tools. On Ubuntu install them with
`sudo apt install webp`; the package provides `cwebp`.

## Current MVP endpoints

User-owned endpoints require an `X-User-Id` header. This is an MVP auth boundary
that can later be replaced with JWT without changing the ownership model.

```text
GET  /api/health
POST /api/uploads                         X-User-Id required
GET  /api/media                           X-User-Id required
GET  /api/media/{id}                      X-User-Id required
GET  /api/media/{id}/file                 X-User-Id required
POST /api/stickers/from-video             X-User-Id required
POST /api/stickers/from-image             X-User-Id required
GET  /api/stickers                        X-User-Id required, owned stickers plus ready public stickers
GET  /api/stickers/my                     X-User-Id required
GET  /api/stickers/all                    public ready stickers only
GET  /api/stickers/{id}                   owned or ready public sticker
GET  /api/stickers/{id}/status            owned or ready public sticker
GET  /api/stickers/{id}/download          owned or ready public sticker
GET  /api/telegram/connect                X-User-Id required
POST /api/telegram/webhook                Telegram webhook
POST /api/stickers/{id}/telegram/send     X-User-Id required, sends ready WebP via bot
POST /api/stickers/{id}/favorite          owned or ready public sticker
DELETE /api/stickers/{id}/favorite        owned or ready public sticker
DELETE /api/stickers/{id}                 X-User-Id required, returns isDelete
```

## Create video sticker request

```json
{
  "sourceMediaId": "uploaded-video-id",
  "name": "Funny reaction",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "KeepOriginal",
  "outputFormat": "Mp4",
  "shape": "Original",
  "removeBackground": false,
  "isPublic": false
}
```

Use `"name"` for the display name shown in the UI. If it is omitted, the
backend uses the source file name without extension. Names are trimmed and
limited to 80 characters.
Use `"Mute"` for silent stickers.
Use `"outputFormat": "Gif"` with `"audioMode": "Mute"` to export a silent
looping GIF. If `outputFormat` is omitted, the backend exports MP4.
Use `"outputFormat": "Webp"` with `"audioMode": "Mute"` to export a `.webp`
file. The backend creates a Telegram-compatible static WebP sticker file:
`512x512` max dimensions, one side exactly `512px`, and `512KB` max output
size. Video or GIF sources use the first selected frame. Sending it as a real
Telegram sticker still requires Telegram's sticker import or `sendSticker` flow.
Use `"shape": "Square"` for a square crop, `"shape": "Portrait"` for 4:5, and
`"shape": "Landscape"` for 16:9. Use `"shape": "Circle"` with
`"outputFormat": "Gif"` or `"Webp"` and `"audioMode": "Mute"` for a
transparent circular sticker.
Use `"removeBackground": true` with image/GIF sources and
`"outputFormat": "Gif"` or `"Webp"` to remove a simple solid-color background. Pass
`"backgroundColor": "#ffffff"` or another hex color; if omitted, white is used.
Use `"isPublic": true` when the sticker should appear in `/api/stickers/all`.
Private stickers stay visible only in that user's `/api/stickers/my` list.
`GET /api/stickers` returns the current user's stickers plus ready public
stickers from other users, which is the easiest endpoint for a signed-in
frontend feed.
Sticker responses include `isDelete`; show the delete button only when it is
`true`.
Sticker responses include `isFavorite` for the current user. Use
`POST /api/stickers/{id}/favorite` to set it true and
`DELETE /api/stickers/{id}/favorite` to set it false.

## Telegram bot setup

Create a Telegram bot with BotFather and configure the token through environment
variables or a systemd override:

```ini
[Service]
Environment="Telegram__BotToken=123456:bot-token"
Environment="Telegram__BotUsername=your_bot_username"
Environment="Telegram__WebhookSecretToken=long-random-secret"
```

Register the webhook:

```bash
curl "https://api.telegram.org/bot$Telegram__BotToken/setWebhook" \
  -d "url=https://your-domain.com/api/telegram/webhook" \
  -d "secret_token=$Telegram__WebhookSecretToken"
```

The frontend should call `GET /api/telegram/connect`, open the returned
`startUrl`, then call `POST /api/stickers/{id}/telegram/send` after creating a
ready `"Webp"` sticker.

`POST /api/stickers/from-image` accepts the same request shape. For image
sources, use `"Mute"` or `"UseMedia"` because images do not have original
audio. The image is looped for `trimEndMs - trimStartMs` and exported as MP4,
GIF, or WebP depending on `outputFormat`.

`DELETE /api/stickers/{id}` returns `{ "isDelete": true }` when the sticker
belonged to the current user and was deleted. It returns `{ "isDelete": false }`
when the sticker is missing or belongs to another user.

Uploaded audio, video, and GIF responses include a `preview` block when FFprobe
can read duration and stream information. Video and GIF previews also include a
thumbnail URL generated under `/media/previews`.

Sticker requests validate video and audio trim ranges against preview duration
metadata. The source trim range controls the final output length: longer audio
is trimmed to the sticker duration, and shorter audio is padded with silence.

Use a different part of the source video's audio:

```json
{
  "sourceMediaId": "uploaded-video-id",
  "trimStartMs": 7000,
  "trimEndMs": 12000,
  "audioMode": "KeepOriginal",
  "audioTrimStartMs": 1000,
  "audioTrimEndMs": 6000
}
```

Use audio from another uploaded audio or video file:

```json
{
  "sourceMediaId": "uploaded-video-id",
  "trimStartMs": 7000,
  "trimEndMs": 12000,
  "audioMode": "UseMedia",
  "audioSourceMediaId": "uploaded-audio-or-video-id",
  "audioTrimStartMs": 2000,
  "audioTrimEndMs": 7000
}
```
