# SoundSticker

ASP.NET Core backend for the sound sticker MVP.

## What works now

- Upload local media files.
- Return preview metadata for timeline controls after upload.
- Store uploaded media under `storage/originals`.
- Create a video sticker job from an uploaded video.
- Keep original video audio, mute it, or use audio from another uploaded audio/video file.
- Trim video and audio tracks from different time ranges.
- Process sticker jobs in a background worker.
- Save generated MP4 stickers under `storage/stickers`.
- Serve local media files from `/media`.

## Run locally

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

```text
GET  /api/health
POST /api/uploads
GET  /api/media
GET  /api/media/{id}
POST /api/stickers/from-video
GET  /api/stickers
GET  /api/stickers/{id}
GET  /api/stickers/{id}/status
```

## Create video sticker request

```json
{
  "sourceMediaId": "uploaded-video-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "KeepOriginal"
}
```

Use `"Mute"` for silent stickers.

Uploaded audio, video, and GIF responses include a `preview` block when FFprobe
can read duration and stream information. Video and GIF previews also include a
thumbnail URL generated under `/media/previews`.

Sticker requests validate trim ranges against preview duration metadata. The
video trim range controls the final output length: longer audio is trimmed to
the video duration, and shorter audio is padded with silence.

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
