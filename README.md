# MiniTube

- `src/` - .NET 8 API (this file)
- `frontend/` - React app, see `frontend/README.md` (`cd frontend && npm install && npm run dev`)

# Backend

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
# Already applied earlier migrations? Add another one instead (this step: search columns):
#   dotnet ef migrations add AddSearch -p src/MiniTube.Infrastructure -s src/MiniTube.Api -o Persistence/Migrations

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

### Playlists

- Visibility works like videos: **Private** (owner), **Unlisted** (anyone with the link), **Public** (listed on `GET /api/playlists`, and only if it has at least one video visitors can see).
- Every user has one built-in **Watch later** playlist (`kind: "WatchLater"`, always private, cannot be renamed/deleted). `GET /api/playlists/watch-later` returns it and creates it on first use. Localise its title in the UI by `kind`.
- A video appears **once** per playlist (adding it again is a no-op that returns the existing item). Limits: 100 custom playlists per user, 5000 videos per playlist.
- You can only add videos you may watch (Ready, and not someone else's private video).
- Viewers only see items whose video they may watch: if a video later becomes private/unavailable it silently disappears for others, and `itemCount` / `thumbnailUrl` are computed per viewer.
- **"Save to playlist" dialog:** `GET /api/playlists/mine?videoId={id}` returns every playlist with `containsVideo` true/false. Toggle with `POST /api/playlists/{id}/items` or `DELETE /api/playlists/{id}/videos/{videoId}`.
- **Reordering (drag and drop):** `PUT /api/playlists/{id}/items/{itemId}/move` with `{ "afterItemId": "<the item it now sits under>" }` (null = top). `position` is only a sort key: increasing but may have gaps, so always sort by it instead of using it as an index.
- **Playing a playlist:** `GET /api/playlists/{id}/items?pageSize=100` gives the queue; the React watch page keeps `?list={id}` and moves to the next item when a video ends.

### Search

- `GET /api/search/videos?q=&sort=&uploadDate=&duration=&ownerId=&page=&pageSize=`
  - `sort`: `Relevance` (default) | `Newest` | `MostViewed` | `MostLiked`
  - `uploadDate`: `Any` | `Today` (24 h) | `Week` | `Month` | `Year`
  - `duration`: `Any` | `Short` (< 4 min) | `Medium` (4-20 min) | `Long` (> 20 min)
  - Only **Public + Ready** videos are searchable (unlisted ones are link-only). Without `q` it just browses with the filters.
- `GET /api/search/channels?q=` users that have at least one public video, most videos first.
- `GET /api/search/suggestions?q=` autocomplete: up to 6 video titles + 2 channels (`type: "video" | "channel"`). Debounce ~250 ms and cancel the previous request in the search box.
- `POST /api/admin/search/reindex` (admin) rebuilds the search columns.

How it works:
- Title + description are stored a second time as **lower-case, accent-free text** (`Hướng dẫn học lập trình` -> `huong dan hoc lap trinh`, `đ` -> `d`), so people can type without accents. The columns (`Videos.SearchTitle/SearchText`, `Users.SearchName`) are kept up to date on create/update, and a startup job fills them for rows that existed before (check the log: "Search index backfill").
- Every word of the query must appear (AND). Relevance = phrase/title matches first, then popularity (views, likes) and a small freshness bonus, computed over the 300 most popular matches, so deep pages stop at 300 results.
- Default mode is a portable `LIKE` search (fine for thousands of videos, works on LocalDB). For a big library switch to SQL Server full-text:
  1. Run once in SSMS (full-text can't be created inside a normal migration; LocalDB doesn't support it):
     ```sql
     CREATE FULLTEXT CATALOG MiniTubeCatalog;
     CREATE FULLTEXT INDEX ON dbo.Videos(SearchText LANGUAGE 0)
         KEY INDEX PK_Videos ON MiniTubeCatalog WITH CHANGE_TRACKING AUTO;
     ```
  2. Set `"Search": { "UseFullText": true }`. Words then match from their start (`hoc*`) instead of anywhere inside a word.

### Recommendations (no extra migration)

| Endpoint | Use |
|---|---|
| `GET /api/videos/{id}/related?limit=20` | watch-page sidebar / "up next" (first item). Anonymous OK, not personalised |
| `GET /api/feed/trending?window=Day\|Week\|Month` | trending page |
| `GET /api/feed/recommended` | home feed. With a token: personalised; without: trending (Week) |
| `GET /api/feed/continue-watching?limit=20` | "Continue watching" row (items carry `positionSeconds` to resume at) |

How it ranks (plain rules, no ML):
- **Related** = candidates from (a) videos sharing the most distinctive title words, (b) the same channel, (c) **co-watching** (what people who watched this also watched, from watch history). Score = title similarity + same-channel bonus + co-watch count + popularity (views, likes) + a freshness bonus. Topped up with popular videos so it is never empty.
- **Trending** = counted views + 5 x likes that happened inside the window (from `VideoViews` / `VideoReactions`), then filled with all-time popular videos on a quiet site.
- **Home feed** = interest profile built from your likes (weight 3), finished videos (2) and watched-for-30 s videos (1), as favourite title words and channels; dislikes subtract. Candidates come from your top words and channels plus some fresh/popular videos for exploration. Excluded: videos you finished, liked, disliked or uploaded. Max 3 per channel at the top for variety. New users with no signals get trending.
- Only **Public + Ready** videos are recommended. Ranked id lists are cached in memory for 5 minutes (10 for related), so paging is stable; visibility is re-checked when cards are loaded. Restarting the API clears the cache.
- "Words" are the accent-free title tokens from the search step (stopwords and bare numbers ignored). The weights are constants at the top of `RecommendationService` and easy to tune.

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
| POST | /api/playlists | Bearer |
| GET | /api/playlists | – (public playlists; `ownerId`, `page`, `pageSize`) |
| GET | /api/playlists/mine | Bearer (`videoId` adds `containsVideo`) |
| GET | /api/playlists/watch-later | Bearer |
| GET | /api/playlists/{id} | – (private: owner/admin) |
| GET | /api/playlists/{id}/items | – (private: owner/admin) |
| PUT / DELETE | /api/playlists/{id} | Bearer (owner; admin may delete) |
| POST | /api/playlists/{id}/items | Bearer (owner) |
| DELETE | /api/playlists/{id}/items/{itemId} | Bearer (owner) |
| DELETE | /api/playlists/{id}/videos/{videoId} | Bearer (owner) |
| PUT | /api/playlists/{id}/items/{itemId}/move | Bearer (owner) |
| GET | /api/search/videos | – |
| GET | /api/search/channels | – |
| GET | /api/search/suggestions | – |
| POST | /api/admin/search/reindex | Admin |
| GET | /api/videos/{id}/related | – |
| GET | /api/feed/trending | – |
| GET | /api/feed/recommended | – (personalised with a token) |
| GET | /api/feed/continue-watching | Bearer |
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
