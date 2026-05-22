# Stickers

Backend playground for creating short sound stickers from uploaded media.

`SoundSticker` is an ASP.NET Core MVP that accepts local media uploads, stores
them on disk, and uses FFmpeg in a background worker to build short MP4 sticker
clips. Video and audio can be trimmed independently, so a sticker can keep its
original audio, become silent, or use audio from another uploaded audio or video
file.

## Highlights

- Upload video, audio, image, and GIF files.
- Keep original uploads under local storage.
- Create video sticker jobs with a maximum duration of 5 seconds.
- Trim the video track and audio track from different time ranges.
- Reuse audio from the source video or attach audio from another uploaded file.
- Process stickers asynchronously and query their status.
- Serve generated sticker files through the local `/media` path.
- Explore the API through Swagger during development.

## Stack

- ASP.NET Core on .NET 10
- Minimal APIs
- Swashbuckle Swagger UI
- FFmpeg media processing
- In-memory metadata repository for the current MVP

## Project Layout

```text
Stickers/
|-- SoundSticker.slnx
|-- README.md
`-- SoundSticker/
    |-- Contracts/
    |-- Domain/
    |-- Persistence/
    |-- Processing/
    |-- Storage/
    `-- Program.cs
```

## Run Locally

Requirements:

- .NET 10 SDK
- FFmpeg available in `PATH`, or an explicit `Ffmpeg:ExecutablePath`

Start the API:

```powershell
dotnet run --project SoundSticker/SoundSticker.csproj --launch-profile http
```

Then open:

```text
http://localhost:5258/swagger
http://localhost:5258/api/health
```

If FFmpeg is not available in `PATH`, configure it in app settings:

```json
{
  "Ffmpeg": {
    "ExecutablePath": "C:\\path\\to\\ffmpeg.exe"
  }
}
```

## API Surface

```text
GET  /api/health
POST /api/uploads
GET  /api/media
GET  /api/media/{id}
GET  /api/media/{id}/file
POST /api/stickers/from-video
GET  /api/stickers
GET  /api/stickers/{id}
GET  /api/stickers/{id}/status
```

## Sticker Requests

Create a 5-second sticker with the source video's matching audio:

```json
{
  "sourceMediaId": "uploaded-video-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "KeepOriginal"
}
```

Use one time range for video and a different time range for audio from the same
video:

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

Attach audio from another uploaded audio or video file:

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

Use `"Mute"` as the audio mode for a silent sticker.

## Current MVP Notes

- Uploaded files and generated stickers are stored on disk.
- Media and sticker metadata are currently kept in memory.
- Restarting the API clears the metadata lists even when stored files still
  exist locally.
- Sticker export currently produces MP4 video output.
