# SoundSticker Frontend API Handoff

This document describes the current backend contract for a frontend client.
It reflects the current MVP API implementation.

## Base URL

Local HTTP launch profile:

```text
http://localhost:5258
```

Development tools:

```text
Swagger: http://localhost:5258/swagger
Health:  http://localhost:5258/api/health
```

All JSON enum values are serialized as strings.

## What The Backend Can Do

- Upload video, audio, image, and GIF files.
- Store original uploaded files and return public media URLs.
- Return preview metadata for audio, video, and GIF files when FFprobe can read
  them.
- Generate a thumbnail URL for uploaded video and GIF files.
- Create a video sticker job from an uploaded video.
- Use matching audio from the source video.
- Mute the sticker.
- Use audio from another uploaded audio or video file.
- Trim video and audio from different time ranges.
- Process sticker jobs asynchronously with queue/status polling.
- Serve uploaded files, thumbnails, and generated sticker MP4 files from
  `/media/...`.

## Current MVP Limits

- Sticker output is MP4 video.
- Sticker video duration is limited to 5 seconds by current config.
- Metadata is stored in memory. Restarting the backend clears media and sticker
  lists even if files still exist on disk.
- Creating a sticker currently requires source preview duration metadata.
  FFprobe must be configured before uploading media used for sticker creation.
- There is no delete endpoint yet.
- There is no progress percentage yet. Poll status instead.
- `CoverImageId` exists in the request/response contract, but the current video
  processor does not use the cover image during export yet.

## Recommended Frontend Workflow

1. Check backend health with `GET /api/health`.
2. Upload the main video with `POST /api/uploads`.
3. Read `media.preview.durationMs` and show a video trim range control.
4. For custom audio, upload another audio or video file and use its media id.
5. Create a sticker job with `POST /api/stickers/from-video`.
6. Poll `GET /api/stickers/{id}/status` until status is `Ready` or `Failed`.
7. When ready, play or download `outputUrl`.

## Shared Types

### MediaKind

```text
Image
Gif
Audio
Video
```

`Unknown` exists internally, but accepted uploads should return one of the media
types above.

### StickerAudioMode

```text
KeepOriginal
Mute
UseMedia
```

Meaning:

- `KeepOriginal`: use audio from `sourceMediaId`.
- `Mute`: create a silent sticker.
- `UseMedia`: use audio from `audioSourceMediaId`.

### StickerStatus

```text
Queued
Processing
Ready
Failed
```

## Response Shapes

### ProblemResponse

Most validation errors return:

```json
{
  "message": "Human readable error message."
}
```

### MediaFileResponse

```json
{
  "id": "3b89e8d8-a1bd-4cb9-b98f-2b86fd7807d5",
  "originalFileName": "clip.mp4",
  "kind": "Video",
  "contentType": "video/mp4",
  "sizeBytes": 1299224,
  "url": "/media/originals/3b89e8d8a1bd4cb9b98f2b86fd7807d5.mp4",
  "preview": {
    "durationMs": 18420,
    "width": 1080,
    "height": 1920,
    "hasAudio": true,
    "thumbnailUrl": "/media/previews/3b89e8d8a1bd4cb9b98f2b86fd7807d5.jpg"
  },
  "createdAt": "2026-05-22T18:30:00+00:00"
}
```

Notes:

- `preview` can be `null`.
- `durationMs`, `width`, `height`, and `thumbnailUrl` can be `null`.
- Audio uploads can have duration and `hasAudio`, but no thumbnail.
- Video/GIF uploads can have a thumbnail URL.
- Use `url` and `thumbnailUrl` as paths relative to the backend base URL.

### StickerResponse

```json
{
  "id": "59936b11-4a0c-4f62-bc2f-bf6516e23f81",
  "sourceMediaId": "3b89e8d8-a1bd-4cb9-b98f-2b86fd7807d5",
  "coverImageId": null,
  "status": "Queued",
  "audioMode": "UseMedia",
  "audioSourceMediaId": "269e1b4c-4156-4447-aa6b-69b4b3e27aca",
  "trimStartMs": 7000,
  "trimEndMs": 12000,
  "audioTrimStartMs": 2000,
  "audioTrimEndMs": 7000,
  "durationMs": 5000,
  "outputUrl": null,
  "errorMessage": null,
  "createdAt": "2026-05-22T18:30:00+00:00",
  "completedAt": null
}
```

### StickerStatusResponse

```json
{
  "id": "59936b11-4a0c-4f62-bc2f-bf6516e23f81",
  "status": "Ready",
  "errorMessage": null,
  "outputUrl": "/media/stickers/59936b114a0c4f62bc2fbf6516e23f81.mp4"
}
```

## Endpoints

### GET /api/health

Purpose:

- Check whether the backend is running.

Success:

- `200 OK`

Example response:

```json
{
  "status": "ok",
  "checkedAt": "2026-05-22T18:30:00+00:00"
}
```

### POST /api/uploads

Purpose:

- Upload a local media file.

Request:

- Content type: `multipart/form-data`
- Form field name: `file`

Accepted media:

- Video: `.mp4`, `.mov`, `.webm`, `.mkv`
- Audio: `.mp3`, `.wav`, `.m4a`, `.aac`, `.ogg`
- Images: `.jpg`, `.jpeg`, `.png`, `.webp`
- GIF: `.gif`

Success:

- `201 Created`
- Body: `MediaFileResponse`

Frontend example:

```ts
const body = new FormData();
body.append("file", file);

const response = await fetch(`${baseUrl}/api/uploads`, {
  method: "POST",
  body
});

const media = await response.json();
```

Possible validation errors:

- Empty file.
- File larger than configured upload limit.
- Unsupported media type.

### GET /api/media

Purpose:

- List uploaded media known to the current backend process.

Success:

- `200 OK`
- Body: `MediaFileResponse[]`

### GET /api/media/{id}

Purpose:

- Get one uploaded media item by id.

Success:

- `200 OK`
- Body: `MediaFileResponse`

Missing media:

- `404 Not Found`

### GET /api/media/{id}/file

Purpose:

- Stream the original stored file through an API route.

Success:

- File response with the stored content type.

Missing media or missing file:

- `404 Not Found`

Note:

- The `MediaFileResponse.url` value is also publicly served from `/media/...`.

### POST /api/stickers/from-video

Purpose:

- Queue a video sticker export job.

Success:

- `202 Accepted`
- Body: `StickerResponse`

Required fields:

```json
{
  "sourceMediaId": "uploaded-video-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "KeepOriginal"
}
```

Optional fields:

- `coverImageId`
- `audioSourceMediaId`
- `audioTrimStartMs`
- `audioTrimEndMs`

#### Original Audio Request

Use source video frames and source video audio from matching default ranges:

```json
{
  "sourceMediaId": "uploaded-video-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "KeepOriginal"
}
```

If `audioTrimStartMs` and `audioTrimEndMs` are omitted, they default to the
video trim range.

#### Different Source Video Audio Range

Use video seconds 7-12 and audio seconds 1-6 from the same source video:

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

#### Silent Sticker Request

```json
{
  "sourceMediaId": "uploaded-video-id",
  "trimStartMs": 7000,
  "trimEndMs": 12000,
  "audioMode": "Mute"
}
```

#### Custom Audio Request

Use video seconds 7-12 and another uploaded media item's audio seconds 2-7:

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

Custom audio source requirements:

- `audioSourceMediaId` is required for `UseMedia`.
- Audio source media must be `Audio` or `Video`.
- Audio source media must contain an audio stream.

Trim validation rules:

- `trimStartMs` must be `>= 0`.
- `trimEndMs` must be greater than `trimStartMs`.
- Video trim duration must not exceed the configured max sticker duration.
- Video trim end must not exceed source video duration.
- Non-muted audio trim start/end must be valid.
- Audio trim end must not exceed its selected audio source duration.

Audio duration behavior:

- Final sticker duration comes from the video trim range.
- Long audio is trimmed to the video duration.
- Short audio is padded with silence until the video ends.

Common sticker validation errors:

- Source media does not exist.
- Source media is not video.
- Source or audio preview metadata is unavailable.
- Source video has no audio when `KeepOriginal` is requested.
- Audio source does not exist.
- Audio source has no audio stream.
- Invalid audio mode.
- Invalid trim range.

### GET /api/stickers

Purpose:

- List sticker jobs known to the current backend process.

Success:

- `200 OK`
- Body: `StickerResponse[]`

### GET /api/stickers/{id}

Purpose:

- Get one sticker job with its full sticker response.

Success:

- `200 OK`
- Body: `StickerResponse`

Missing sticker:

- `404 Not Found`

### GET /api/stickers/{id}/status

Purpose:

- Poll one sticker job during background processing.

Success:

- `200 OK`
- Body: `StickerStatusResponse`

Missing sticker:

- `404 Not Found`

Recommended polling behavior:

- Poll every 1-2 seconds after receiving `202 Accepted`.
- Stop polling when status is `Ready` or `Failed`.
- When `Ready`, use `outputUrl`.
- When `Failed`, show `errorMessage`.

## Frontend UI Notes

### Upload Screen

- Use one upload control for the main video.
- Use a second optional upload control for custom audio.
- Audio source can be an audio file or another video file.

### Timeline Controls

- Use `preview.durationMs` as the upper bound.
- Keep times in milliseconds in API requests.
- Disable sticker creation when required preview duration is missing.
- For `KeepOriginal`, disable audio options that require source audio when
  `preview.hasAudio` is false.

### Preview Rendering

- Play uploaded video from `media.url`.
- Show thumbnail from `media.preview.thumbnailUrl` when available.
- Generated sticker playback uses `sticker.outputUrl` after status becomes
  `Ready`.

### Error Handling

- Read `message` from backend `400` validation responses.
- Treat `404` as stale in-memory state or a missing id.
- A backend restart can clear media/sticker API lists in the current MVP.

## Implementation Checklist For Frontend

- Configure `baseUrl`.
- Add media upload with multipart field name `file`.
- Store returned media ids.
- Render uploaded media preview information.
- Build video range and optional audio range controls in milliseconds.
- Support three audio modes: original, mute, custom media.
- Submit sticker job.
- Poll sticker status.
- Render ready MP4 output.
- Show backend validation messages.
