# MiniTube – Backend (Identity / User management)

.NET 8 · Clean Architecture · EF Core + SQL Server · ASP.NET Core Identity · JWT + rotating refresh tokens

## Features:
- Asynchronous processing to generate preview thumbnails and different resolutions of the uploaded videos
- Video upload and management
- Video playlist creation and management
- Video playback
- Video recommendations and suggestions
- Search and discovery of videos
- User authentication and authorization
- JWT token
- User profile
- User watch history
and more...

## Tech:
- Net 8, net core web api
- EF Core, MsSQL database
- React 18, Vite

## Frontend: like Youtube!
- UI UX Pro Max
- Light/ dark mode
- Multi languge (Vietnames/ English)

## Setup

```bash
cd MiniTube
dotnet new sln -n MiniTube
dotnet sln add src/MiniTube.Domain src/MiniTube.Application src/MiniTube.Infrastructure src/MiniTube.Api

# JWT secret (don't keep real secrets in appsettings.json)
cd src/MiniTube.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"

# Migration (run from the solution root)
cd ../..
dotnet tool install --global dotnet-ef   # once
dotnet ef migrations add InitialIdentity -p src/MiniTube.Infrastructure -s src/MiniTube.Api -o Persistence/Migrations
# Already applied earlier migrations? Add another one instead (this step: reactions, history, views):
#   dotnet ef migrations add AddPlayback -p src/MiniTube.Infrastructure -s src/MiniTube.Api -o Persistence/Migrations

dotnet run --project src/MiniTube.Api    # Development: auto-migrates + seeds roles & admin
```

Swagger: https://localhost:7100/swagger
Seeded admin (Development only): `admin` / `Admin@12345` – change it.

## FFmpeg (needed for videos)

Install FFmpeg locally so that both `ffmpeg` and `ffprobe` are on PATH (check with `ffmpeg -version`):

- Windows: `winget install Gyan.FFmpeg` (then open a new terminal), or `choco install ffmpeg`
- macOS: `brew install ffmpeg`
- Ubuntu/Debian: `sudo apt install ffmpeg`

Not on PATH? Set `Video:FfmpegPath` / `Video:FfprobePath` to the full paths of the executables.
On startup the API logs whether FFmpeg was found.

## Videos

### Upload flow (2 calls, so the React app can show a real progress bar with XHR)

1. `POST /api/videos` `{ "title", "description", "visibility": "Private|Unlisted|Public" }` -> returns the video (`status: PendingUpload`)
2. `PUT /api/videos/{id}/source?fileName=holiday.mp4` with the **raw file bytes** as the body (not multipart). It is streamed to disk.
3. The video becomes `Queued` -> `Processing` (poll `GET /api/videos/{id}`, owners get `processingProgress` 0-100) -> `Ready` or `Failed` (`processingError`).

Both calls need a confirmed email (`VerifiedEmail` policy). The seeded dev admin is already confirmed.

```bash
TOKEN=...   # from /api/auth/login
curl -k -X POST https://localhost:7100/api/videos -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" -d '{"title":"Test","visibility":"Public"}'
curl -k -X PUT "https://localhost:7100/api/videos/<id>/source?fileName=test.mp4" \
     -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/octet-stream" --data-binary @test.mp4
```

### What the worker produces

- `thumb.jpg` (640x360, taken ~25% into the video) -> public, `/media/videos/{id}/thumb.jpg`
- HLS adaptive streaming: `master.m3u8` + one playlist per rendition (1080p / 720p / 480p / 360p, never upscaled; tiny sources get a single native-size rendition), 4-second `.ts` segments. Kept in the **private** storage root and served through `/api/stream/{id}/...`, which checks visibility.
- Statuses: `PendingUpload -> Uploading -> Queued -> Processing -> Ready | Failed`.
- The database is the source of truth: after a crash/restart, `Processing` videos are re-queued automatically. Run a single API instance (the queue is in-memory + DB claim).

### Playback tracking (views, history, likes)

How the React player should use it:

| When | Call |
|---|---|
| Open the watch page | `GET /api/videos/{id}` -> use `resumePositionSeconds`, `myReaction`, `viewCount`, `likeCount` |
| After the viewer really watched `min(30 s, 50 % of duration)` (once per page view) | `POST /api/videos/{id}/views` (anonymous OK) |
| Every ~15 s while playing, and on pause / seek end / tab hidden / page leave | `PUT /api/videos/{id}/progress` `{ "positionSeconds": 73.5 }` (signed in) |
| Like button | `PUT /api/videos/{id}/reaction` `{ "type": "Like" \| "Dislike" \| null }` |
| Pages | `GET /api/videos/liked`, `GET /api/users/me/history`, `DELETE /api/users/me/history[/{videoId}]` |
| Privacy toggle | `PUT /api/users/me/history/settings` `{ "paused": true }` (also exposed as `watchHistoryPaused` in `/api/users/me`) |

Rules:
- A view counts once per viewer per 24 h. Signed-in viewers are identified by account; anonymous viewers (and users who paused their history) only by a one-way hash of IP + user agent. The count is **client-reported**, so it is a good-enough heuristic, not tamper-proof.
- A video counts as completed at 95 %; `resumePositionSeconds` is null for completed videos or when less than 5 s were watched.
- `dislikeCount` is returned to the owner/admin only (like YouTube Studio); everyone else sees `likeCount`.
- Private videos of other users cannot be tracked, liked or listed in history/likes.
- On page leave use `fetch(url, { method: 'PUT', keepalive: true, headers: { Authorization } })`: `navigator.sendBeacon` cannot send an Authorization header.
- Counters (`viewCount`, `likeCount`) are denormalised on `Videos` and updated with atomic SQL increments.

### Try playback

Run the API in Development and open `https://localhost:7100/player-test.html`, paste the video id (and a token for Private videos).

### Folder layout

```
storage/            (public, served at /media)   avatars/..., videos/{id}/thumb.jpg
storage-private/    (never served)               videos/{id}/source/original.mp4, videos/{id}/hls/...
```
`Storage:PrivateRootPath` must not be inside `Storage:RootPath` (the app refuses to start otherwise).

### Config (`Video:*`)

`MaxUploadBytes` (2 GB), `AllowedExtensions` (.mp4 .mov .m4v .mkv .webm .avi), `X264Preset` (veryfast; use `ultrafast` for quicker tests, `medium` for better compression), `MaxConcurrency` (1), `TimeoutMinutes`, `MaxDurationMinutes`, `KeepOriginal` (true).

## Email

- No `Email:Host` configured -> emails are **printed in the console log** (copy the link from there).
- Real SMTP, e.g. Brevo: `Email:Host=smtp-relay.brevo.com`, `Port=587`, `UserName`, `Password` (use user-secrets), `FromAddress` = a verified sender.
- Links point to `Frontend:BaseUrl` -> `/confirm-email?userId=..&token=..` and `/reset-password?email=..&token=..`
- Email language follows the `Accept-Language` header (`en...` = English, anything else = Vietnamese).
  Make the React axios client send the language selected in the app.

## Endpoints

| Method | Route | Auth |
|---|---|---|
| POST | /api/auth/register | – |
| POST | /api/auth/login | – |
| POST | /api/auth/refresh | – |
| POST | /api/auth/logout | – |
| POST | /api/auth/logout-all | Bearer |
| POST | /api/auth/confirm-email | – |
| POST | /api/auth/resend-confirmation | – (5 / 10 min / IP) |
| POST | /api/auth/forgot-password | – (5 / 10 min / IP) |
| POST | /api/auth/reset-password | – |
| GET / PUT | /api/users/me | Bearer |
| PUT / DELETE | /api/users/me/avatar | Bearer (multipart `file`) |
| POST | /api/users/me/change-password | Bearer |
| GET | /api/users/{id} | – (public profile) |
| POST | /api/videos | Bearer + verified email |
| PUT | /api/videos/{id}/source | Bearer + verified email (raw body) |
| GET | /api/videos/{id} | – (private: owner/admin) |
| GET | /api/videos | – (public ready videos; `ownerId`, `page`, `pageSize`) |
| GET | /api/videos/mine | Bearer |
| PUT / DELETE | /api/videos/{id} | Bearer (owner/admin) |
| POST | /api/videos/{id}/reprocess | Bearer (owner/admin, Failed only) |
| GET | /api/stream/{id}/master.m3u8 (+ renditions, segments) | – (private: Bearer owner/admin) |
| POST | /api/videos/{id}/views | – (counted once / viewer / 24 h) |
| PUT | /api/videos/{id}/progress | Bearer |
| PUT | /api/videos/{id}/reaction | Bearer |
| GET | /api/videos/liked | Bearer |
| GET / DELETE | /api/users/me/history | Bearer |
| DELETE | /api/users/me/history/{videoId} | Bearer |
| PUT | /api/users/me/history/settings | Bearer |
| GET | /api/admin/users | Admin |
| PUT | /api/admin/users/{id}/lock | Admin |
| PUT | /api/admin/users/{id}/roles | Admin |

Error responses are ProblemDetails; some include `"code"` (`invalid_token`, `account_locked`) for the frontend to switch on.

## Notes

- Unconfirmed users can log in, but endpoints guarded by `[Authorize(Policy = Policies.VerifiedEmail)]` (use it for video upload) need `email_verified=true` in the access token -> after confirming, the client calls `/api/auth/refresh`.
- Avatars: JPEG/PNG/WebP up to 2 MB, decoded and re-encoded to 256x256 WebP (EXIF stripped), saved under `Storage:RootPath` and served from `/media/avatars/...`. `avatarUrl` in responses is relative: prefix it with the API base URL.
- Security of the media pipeline: uploads are checked by extension, size (counted while streaming), and then by `ffprobe` (container whitelist, must contain a video stream). FFmpeg runs with `-protocol_whitelist file` and arguments are passed as a list, never through a shell. The `/api/stream` path is whitelisted by regex.
- Thumbnails are public by URL (unguessable GUID), like avatars; the video files themselves are protected by the visibility check.
- ImageSharp has a split license (free for personal/open-source use and small companies); check it if you ever monetize.
