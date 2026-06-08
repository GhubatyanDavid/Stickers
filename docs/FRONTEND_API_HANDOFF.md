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
- Create a moving sticker job from an uploaded video or GIF.
- Create an image sticker job by looping an uploaded image into MP4 or GIF.
- Export stickers as original aspect, square, circle, portrait, or landscape.
- Remove simple solid-color backgrounds from image and GIF sources.
- Use matching audio from the source video.
- Mute the sticker.
- Use audio from another uploaded audio or video file.
- Trim video and audio from different time ranges.
- Process sticker jobs asynchronously with queue/status polling.
- Serve uploaded files, thumbnails, and generated sticker MP4/GIF files from
  `/media/...`.

## Current MVP Limits

- Sticker output defaults to MP4 video. GIF output is supported with
  `audioMode: "Mute"` because GIF files cannot contain audio.
- Circle shape requires GIF output so the outside edge can stay transparent.
- Background removal is color-key based, works best on simple solid-color
  backgrounds, and currently supports only image/GIF sources with GIF output.
  It is not AI person/object segmentation.
- Sticker duration is limited by `Sticker:MaxDurationMs` (`30000` ms in the
  committed app settings).
- Media and sticker metadata are stored in PostgreSQL when the default
  development settings are used. In-memory persistence is only for temporary
  local testing.
- Creating a video sticker currently requires source preview duration metadata.
  FFprobe must be configured before uploading video media used for sticker
  creation. Still image stickers do not require source preview duration.
- There is no progress percentage yet. Poll status instead.
- `CoverImageId` exists in the request/response contract, but the current video
  processor does not use the cover image during export yet.

## Recommended Frontend Workflow

1. Check backend health with `GET /api/health`.
2. Upload the main video or image with `POST /api/uploads`.
3. For videos, read `media.preview.durationMs` and show a trim range control.
   For images, choose a sticker duration and send it as `trimEndMs - trimStartMs`.
4. For custom audio, upload another audio or video file and use its media id.
5. Create a sticker job with `POST /api/stickers/from-video` or
   `POST /api/stickers/from-image`.
6. Poll `GET /api/stickers/{id}/status` until status is `Ready` or `Failed`.
7. When ready, play `outputUrl` or download through `downloadUrl` using
   `outputFileName`.

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

### StickerOutputFormat

```text
Mp4
Gif
```

Meaning:

- `Mp4`: export an MP4 video sticker. This is the default when omitted.
- `Gif`: export a silent looping GIF sticker. Use `audioMode: "Mute"`.

### StickerShape

```text
Original
Square
Circle
Portrait
Landscape
```

Meaning:

- `Original`: preserve the source aspect ratio. This is the default when
  omitted.
- `Square`: center-crop to a 1:1 sticker.
- `Circle`: center-crop to a transparent circular GIF sticker.
- `Portrait`: center-crop to a 4:5 sticker.
- `Landscape`: center-crop to a 16:9 sticker.

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
  "outputFormat": "Mp4",
  "shape": "Original",
  "removeBackground": false,
  "backgroundColor": null,
  "backgroundSimilarity": 0.18,
  "backgroundBlend": 0.08,
  "isPublic": false,
  "isFavorite": false,
  "isDelete": true,
  "sourceType": "video",
  "outputUrl": null,
  "downloadUrl": null,
  "outputFileName": null,
  "errorMessage": null,
  "createdAt": "2026-05-22T18:30:00+00:00",
  "completedAt": null
}
```

Notes:

- `isDelete: true` means the current `X-User-Id` owns this sticker and the UI
  can show a delete button.
- `isDelete: false` means the sticker is visible but cannot be deleted by the
  current viewer.

### StickerStatusResponse

```json
{
  "id": "59936b11-4a0c-4f62-bc2f-bf6516e23f81",
  "status": "Ready",
  "outputFormat": "Mp4",
  "shape": "Original",
  "errorMessage": null,
  "outputUrl": "/media/stickers/59936b114a0c4f62bc2fbf6516e23f81.mp4",
  "downloadUrl": "/api/stickers/59936b11-4a0c-4f62-bc2f-bf6516e23f81/download",
  "outputFileName": "59936b114a0c4f62bc2fbf6516e23f81.mp4"
}
```

### DeleteStickerResponse

```json
{
  "isDelete": true
}
```

Notes:

- `isDelete: true` means the sticker belonged to the current user and was
  deleted.
- `isDelete: false` means the sticker was missing or did not belong to the
  current user.

### StickerFavoriteResponse

```json
{
  "isFavorite": true
}
```

Notes:

- `isFavorite: true` means the current `X-User-Id` favorited the sticker.
- `isFavorite: false` means the favorite was removed.

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

- Queue a sticker export job from an uploaded video, GIF, or image. This
  endpoint is backward-compatible with existing clients that already post image
  sources to the video route.

Success:

- `202 Accepted`
- Body: `StickerResponse`

Required fields:

```json
{
  "sourceMediaId": "uploaded-video-or-image-id",
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
- `outputFormat` (`"Mp4"` default, or `"Gif"`)
- `shape` (`"Original"` default, `"Square"`, `"Circle"`, `"Portrait"`,
  `"Landscape"`)
- `removeBackground` (`false` default)
- `backgroundColor` hex color such as `"#ffffff"` or `"0x00ff00"`; default is
  white when background removal is enabled.
- `backgroundSimilarity` (`0.18` default, clamped to `0.01`-`1.0`)
- `backgroundBlend` (`0.08` default, clamped to `0.0`-`1.0`)
- `isPublic`

Set `"isPublic": true` when the sticker should appear in the public feed after
processing reaches `Ready`.

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

#### GIF Sticker Request

GIF output is silent and loops forever:

```json
{
  "sourceMediaId": "uploaded-video-gif-or-image-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "Mute",
  "outputFormat": "Gif"
}
```

#### Square Sticker Request

Center-crop the source into a 1:1 MP4 sticker:

```json
{
  "sourceMediaId": "uploaded-video-or-image-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "Mute",
  "shape": "Square"
}
```

#### Circle Sticker Request

Circle stickers use GIF output so the outside edge is transparent:

```json
{
  "sourceMediaId": "uploaded-video-gif-or-image-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "Mute",
  "outputFormat": "Gif",
  "shape": "Circle"
}
```

#### Remove Background From Image Or GIF

This removes a solid-color background and exports a transparent GIF. Use the
dominant background color. If `backgroundColor` is omitted, white is used.

```json
{
  "sourceMediaId": "uploaded-image-or-gif-id",
  "trimStartMs": 0,
  "trimEndMs": 5000,
  "audioMode": "Mute",
  "outputFormat": "Gif",
  "removeBackground": true,
  "backgroundColor": "#ffffff",
  "backgroundSimilarity": 0.18,
  "backgroundBlend": 0.08
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

#### Image Sticker Request

Use an uploaded image as the visual source and attach audio from another
uploaded audio or video file:

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

For image sources:

- The image is looped into the generated output for `trimEndMs - trimStartMs`.
- Use `Mute` or `UseMedia`; `KeepOriginal` is invalid for images.
- Source preview duration metadata is not required.

### POST /api/stickers/from-image

Purpose:

- Queue a sticker export job from an uploaded image. The request and response
  shape are the same as `POST /api/stickers/from-video`.

### Shared Sticker Validation

Custom audio source requirements:

- `audioSourceMediaId` is required for `UseMedia`.
- Audio source media must be `Audio` or `Video`.
- Audio source media must contain an audio stream.

Trim validation rules:

- `trimStartMs` must be `>= 0`.
- `trimEndMs` must be greater than `trimStartMs`.
- Sticker duration must not exceed the configured max sticker duration.
- Video/GIF trim end must not exceed source duration.
- Non-muted audio trim start/end must be valid.
- Audio trim end must not exceed its selected audio source duration.
- GIF output must use `audioMode: "Mute"`.
- Circle shape must use `outputFormat: "Gif"`.
- Background removal must use image/GIF source media.
- Background removal must use `outputFormat: "Gif"`.
- Background color must be a hex color when provided.

Audio duration behavior:

- Final sticker duration comes from the source trim range.
- Long audio is trimmed to the sticker duration.
- Short audio is padded with silence until the sticker ends.

Common sticker validation errors:

- Source media does not exist.
- Source media is not video, GIF, or image.
- Source video/GIF or audio preview metadata is unavailable.
- Source video has no audio when `KeepOriginal` is requested.
- Image source uses `KeepOriginal`.
- GIF output requested with audio.
- Audio source does not exist.
- Audio source has no audio stream.
- Invalid audio mode.
- Invalid output format.
- Invalid sticker shape.
- Background removal requested for an unsupported source or output format.
- Invalid background color.
- Invalid trim range.

### GET /api/stickers

Purpose:

- List stickers visible to the current user.
- Includes all stickers owned by the current `X-User-Id`.
- Also includes ready public stickers created by any other user.

Success:

- `200 OK`
- Body: `StickerResponse[]`

Delete button rule:

- Show delete controls when a sticker item has `isDelete: true`.

Favorite rule:

- Use each sticker item's `isFavorite` to render the favorite state for the
  current user.

### GET /api/stickers/my

Purpose:

- List only stickers owned by the current `X-User-Id`.

Success:

- `200 OK`
- Body: `StickerResponse[]`

Delete button rule:

- All returned stickers belong to the current user, so `isDelete` is `true`.

Favorite rule:

- Use each sticker item's `isFavorite` to render the favorite state for the
  current user.

### GET /api/stickers/all

Purpose:

- Public endpoint for the global sticker feed.
- Returns only stickers that are both `isPublic: true` and `status: "Ready"`.
- Does not require `X-User-Id`.

Success:

- `200 OK`
- Body: `StickerResponse[]`

Delete button rule:

- This public endpoint has no current user context, so `isDelete` is `false`.

Favorite rule:

- This public endpoint has no current user context, so `isFavorite` is `false`.

### GET /api/stickers/{id}

Purpose:

- Get one visible sticker job with its full sticker response.
- A sticker is visible when it is owned by the current `X-User-Id` or is ready
  and public.

Success:

- `200 OK`
- Body: `StickerResponse`

Delete button rule:

- Show delete controls when the returned sticker has `isDelete: true`.

Favorite rule:

- Use `isFavorite` to render the favorite state for the current user.

Missing sticker:

- `404 Not Found`

### GET /api/stickers/{id}/status

Purpose:

- Poll one owned sticker job during background processing.
- Ready public stickers from other users can also be read here.

Success:

- `200 OK`
- Body: `StickerStatusResponse`

Missing sticker:

- `404 Not Found`

Recommended polling behavior:

- Poll every 1-2 seconds after receiving `202 Accepted`.
- Stop polling when status is `Ready` or `Failed`.
- When `Ready`, use `outputUrl` for playback and `downloadUrl` for downloads.
- When `Failed`, show `errorMessage`.

### GET /api/stickers/{id}/download

Purpose:

- Download the generated MP4 or GIF file.
- The backend sets the download filename and content type from the actual saved
  output file extension, so GIF stickers download as `.gif`.

Success:

- `200 OK`
- File response.

Recommended frontend behavior:

- Use `sticker.downloadUrl` as the link target.
- Use `sticker.outputFileName` for the HTML `download` attribute.
- Do not hardcode `.mp4`; GIF stickers should keep `.gif`.

### POST /api/stickers/{id}/favorite

Purpose:

- Mark a visible sticker as favorite for the current `X-User-Id`.

Success:

- `200 OK`
- Body: `StickerFavoriteResponse`

Example response:

```json
{
  "isFavorite": true
}
```

Missing or not visible:

- `404 Not Found`

### DELETE /api/stickers/{id}/favorite

Purpose:

- Remove a visible sticker from favorites for the current `X-User-Id`.

Success:

- `200 OK`
- Body: `StickerFavoriteResponse`

Example response:

```json
{
  "isFavorite": false
}
```

Missing or not visible:

- `404 Not Found`

### DELETE /api/stickers/{id}

Purpose:

- Delete a sticker only if it belongs to the current `X-User-Id`.
- If the sticker is processing, the backend requests cancellation first.

Success:

- `200 OK`
- Body: `DeleteStickerResponse`

Response behavior:

- Owned sticker deleted: `{ "isDelete": true }`
- Missing sticker or another user's sticker: `{ "isDelete": false }`

## Frontend UI Notes

### Upload Screen

- Use one upload control for the main video or image.
- Use a second optional upload control for custom audio.
- Audio source can be an audio file or another video file.

### Timeline Controls

- For video sources, use `preview.durationMs` as the upper bound.
- For image sources, use the selected sticker duration as the source range.
- Keep times in milliseconds in API requests.
- Disable video sticker creation when required preview duration is missing.
- For `KeepOriginal`, disable audio options that require source audio when
  `preview.hasAudio` is false.
- For image sources, hide or disable `KeepOriginal`.

### Preview Rendering

- Play uploaded video from `media.url`.
- Render uploaded images from `media.url`.
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
- Build video/image duration and optional audio range controls in milliseconds.
- Support original, mute, and custom media audio modes; original audio is video-only.
- Submit sticker job.
- Poll sticker status.
- Render ready MP4 output.
- Show backend validation messages.
