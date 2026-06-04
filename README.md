# Stickers

Backend playground for creating short sound stickers from uploaded media.

`SoundSticker` is an ASP.NET Core MVP that accepts local media uploads, stores
them on disk, and uses FFmpeg in a background worker to build short MP4 sticker
clips or silent GIFs. Stickers can be moving video/GIF clips or looped image-based clips with
optional audio. Video and audio can be trimmed independently, so a sticker can
keep its original audio, become silent, or use audio from another uploaded audio
or video file.

## Highlights

- Upload video, audio, image, and GIF files.
- Return duration, video dimensions, audio presence, and video thumbnail data
  for timeline previews after upload.
- Keep original uploads under local storage.
- Create video and image sticker jobs with a maximum duration of 30 seconds.
- Trim the video track and audio track from different time ranges.
- Reuse audio from the source video or attach audio from another uploaded file.
- Loop still images into generated MP4 or GIF stickers.
- Export silent looping GIF stickers with `outputFormat: "Gif"`.
- Export square, circle, portrait, and landscape sticker shapes.
- Process stickers asynchronously and query their status.
- Serve generated sticker files through the local `/media` path.
- Explore the API through Swagger during development.

## Stack

- ASP.NET Core on .NET 8
- Minimal APIs
- Swashbuckle Swagger UI
- FFmpeg media processing
- PostgreSQL metadata persistence through Npgsql

## Project Layout

```text
Stickers/
|-- SoundSticker.slnx
|-- README.md
`-- SoundSticker/
    |-- Contracts/
    |-- Domain/
    |-- FileStorage/
    |-- Persistence/
    |-- Processing/
    `-- Program.cs
```

## Run Locally

Requirements:

- .NET 8 SDK
- PostgreSQL database for media and sticker metadata
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

Configure PostgreSQL in app settings or environment variables:

```json
{
  "Persistence": {
    "Provider": "PostgreSql",
    "ConnectionStringName": "Postgres",
    "AutoCreateSchema": true
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=soundsticker;Username=postgres;Password=postgres"
  }
}
```

Set `Persistence:Provider` to `InMemory` only for temporary local testing.

## API Surface

```text
GET  /api/health
POST /api/uploads
GET  /api/media
GET  /api/media/{id}
GET  /api/media/{id}/file
POST /api/stickers/from-video
POST /api/stickers/from-image
GET  /api/stickers
GET  /api/stickers/my
GET  /api/stickers/all
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
  "audioMode": "KeepOriginal",
  "outputFormat": "Mp4",
  "shape": "Original",
  "isPublic": true
}
```

Use `GET /api/stickers` for the current user's visible feed: that user's own
private/public stickers plus every ready public sticker from other users.
Use `GET /api/stickers/my` when the UI needs only the current user's stickers,
and `GET /api/stickers/all` for the public-ready list without a user header.
Omit `outputFormat` for MP4, or send `"outputFormat": "Gif"` with
`"audioMode": "Mute"` for a silent looping GIF.
Omit `shape` for the source aspect ratio, or send `"Square"`, `"Circle"`,
`"Portrait"`, or `"Landscape"`; circle output should be a muted GIF.
`DELETE /api/stickers/{id}` returns `{ "isDelete": true }` for the current
user's deleted sticker and `{ "isDelete": false }` when it is missing or belongs
to another user.
Sticker list/get responses also include `isDelete`; the frontend can show the
delete button only when that value is `true`.

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

Create a sticker from an uploaded image and another uploaded audio or video
file. The image is looped for the selected duration:

```json
{
  "sourceMediaId": "uploaded-image-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
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

The source trim range controls the final sticker duration. For still images,
the backend loops the image for that duration. If a selected audio clip is
longer than the sticker, audio is trimmed to the sticker duration. If the
selected audio clip is shorter, the rest of the sticker stays silent.

## Current MVP Notes

- Uploaded files and generated stickers are stored on disk.
- Media and sticker metadata are stored in PostgreSQL.
- On startup the API can create the required `media_files` and `stickers`
  tables when `Persistence:AutoCreateSchema` is enabled.
- Deleting a sticker removes its database job record and generated MP4/GIF file
  when one exists.
- Sticker export defaults to MP4 video output and can also produce silent GIF
  output.
