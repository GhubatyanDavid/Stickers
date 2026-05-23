# Stickers

Backend playground for creating short sound stickers from uploaded media.

`SoundSticker` is an ASP.NET Core MVP that accepts local media uploads, stores
them on disk, and uses FFmpeg in a background worker to build short MP4 sticker
clips. Video and audio can be trimmed independently, so a sticker can keep its
original audio, become silent, or use audio from another uploaded audio or video
file.

## Highlights

- Upload video, audio, image, and GIF files.
- Return duration, video dimensions, audio presence, and video thumbnail data
  for timeline previews after upload.
- Keep original uploads under local storage.
- Create video sticker jobs with a maximum duration of 30 seconds.
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
- FFprobe available in `PATH`, or an explicit `Ffmpeg:ProbeExecutablePath` for
  preview duration metadata

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
    "ExecutablePath": "C:\\path\\to\\ffmpeg.exe",
    "ProbeExecutablePath": "C:\\path\\to\\ffprobe.exe"
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
DELETE /api/stickers/{id}
```

## Sticker Requests

Create a sticker with the source video's matching audio:

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

## Media Preview Data

Uploaded audio, video, and GIF media include a `preview` block when FFprobe can
inspect the file:

```json
{
  "id": "uploaded-video-id",
  "url": "/media/originals/video.mp4",
  "preview": {
    "durationMs": 18420,
    "width": 1080,
    "height": 1920,
    "hasAudio": true,
    "thumbnailUrl": "/media/previews/uploaded-video-id.jpg"
  }
}
```

This metadata is intended for video preview screens and timeline range controls.

Sticker creation validates trim ranges against this preview metadata. Upload the
media again after FFprobe is configured if preview metadata is missing.

## Audio Duration Rule

The video trim range controls the final sticker duration. If a selected audio
clip is longer than the video clip, audio is trimmed to the video duration. If
the selected audio clip is shorter, the rest of the sticker stays silent.

## Current MVP Notes

- Uploaded files and generated stickers are stored on disk.
- Deleting a sticker removes its in-memory job record and generated MP4 file
  when one exists.
- Media and sticker metadata are currently kept in memory.
- Restarting the API clears the metadata lists even when stored files still
  exist locally.
- Sticker export currently produces MP4 video output.
