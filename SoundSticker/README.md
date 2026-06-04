# SoundSticker

ASP.NET Core backend for the sound sticker MVP.

## What works now

- Upload local media files.
- Return preview metadata for timeline controls after upload.
- Store uploaded media under `storage/originals`.
- Store media and sticker metadata in PostgreSQL.
- Create a moving sticker job from an uploaded video or GIF.
- Create an image sticker job from an uploaded image by looping it into MP4 or GIF.
- Keep original video audio, mute it, or use audio from another uploaded audio/video file.
- Trim video and audio tracks from different time ranges.
- Process sticker jobs in a background worker.
- Save generated MP4/GIF stickers under `storage/stickers`.
- Mark stickers as private or public during creation.
- Delete old or failed sticker jobs and their generated MP4/GIF files.
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
    "ProbeExecutablePath": "C:\\path\\to\\ffprobe.exe"
  }
}
```

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
DELETE /api/stickers/{id}                 X-User-Id required
```

## Create video sticker request

```json
{
  "sourceMediaId": "uploaded-video-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "KeepOriginal",
  "outputFormat": "Mp4",
  "isPublic": false
}
```

Use `"Mute"` for silent stickers.
Use `"outputFormat": "Gif"` with `"audioMode": "Mute"` to export a silent
looping GIF. If `outputFormat` is omitted, the backend exports MP4.
Use `"isPublic": true` when the sticker should appear in `/api/stickers/all`.
Private stickers stay visible only in that user's `/api/stickers/my` list.
`GET /api/stickers` returns the current user's stickers plus ready public
stickers from other users, which is the easiest endpoint for a signed-in
frontend feed.

`POST /api/stickers/from-image` accepts the same request shape. For image
sources, use `"Mute"` or `"UseMedia"` because images do not have original
audio. The image is looped for `trimEndMs - trimStartMs` and exported as MP4 or
GIF depending on `outputFormat`.

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
