# MiniTube - Frontend

React 18 + TypeScript + Vite. YouTube-style UI with light/dark mode and Vietnamese/English.

## Run

```bash
cd frontend
npm install
npm run dev          # http://localhost:5173
```

Start the .NET API first (`dotnet run --project src/MiniTube.Api`, https://localhost:7100).
In development Vite forwards `/api` and `/media` to the API (no CORS, no certificate warnings).
API on another address? copy `.env.example` to `.env` and set `VITE_API_PROXY`.

| Script | |
|---|---|
| `npm run dev` | dev server |
| `npm run build` | type-check + production build into `dist/` |
| `npm test` | unit + smoke tests (token refresh, formatting, main screens) |

Production: serve `dist/` and `/api` + `/media` from the same origin (reverse proxy), or build with
`VITE_API_URL=https://api.example.com` and add the site origin to `Cors:AllowedOrigins` on the API.

## What is in this version

- **Layout**: header with search + autocomplete, collapsible sidebar (drawer on mobile and on the watch page), responsive grid, skeleton loaders, toasts, keyboard/ARIA support, skip link, reduced-motion support.
- **Light / dark / device theme** (no flash on load) and **Vietnamese / English** (auto-detected, switch in the gear menu; the language is also sent to the API as `Accept-Language`, so emails follow it).
- **Auth**: sign in, register, confirm email, forgot / reset password, protected routes.
- **Home**: "For you" (personalised after you like/watch things) / Popular, Trending (day/week/month), "Continue watching" row, infinite scroll.
- **Watch page**: HLS player with quality menu (Auto/1080p/...), resume, progress heartbeat, view counting, likes, related list with autoplay-next, expandable description, live processing status for the owner.
- **Search**: results with sort / upload date / duration filters, channel hits, suggestions.
- **Upload**: drag and drop, upload progress, then live processing progress; blocks unverified emails with a resend button.
- **Your videos** (studio): status, progress, edit, delete, reprocess failed videos.
- **Channel page**.

## How it works

- `src/api/client.ts`: axios instance. The access token lives in memory, the refresh token in localStorage.
  A token about to expire is renewed **before** a request is sent, a `401` triggers one refresh + retry,
  concurrent refreshes share one request, and a Web Lock keeps several tabs from refreshing at once
  (the API signs everything out if a refresh token is used twice).
- `src/components/Player.tsx`: hls.js (native HLS on Safari). Heartbeat every 15 s and on pause / seek /
  tab hidden / page leave via `fetch(..., { keepalive: true })`. A view is counted once after
  `min(30 s, 50 %)` of real playback (seeking does not count). Private videos send the Bearer token on every segment.
- Server data is cached with TanStack Query; lists use infinite queries.
- Strings live in `src/i18n/en.json` and `vi.json` (keep both in sync).
- Colours are CSS variables in `src/styles/global.css` (`[data-theme='dark']` overrides).

## Known limits

- iPhone Safari plays HLS natively and cannot send an Authorization header, so **private** videos do not play there (public and unlisted do).
- The refresh token in localStorage is readable by injected scripts; keep the app free of XSS (the API-side hardening would be an httpOnly cookie).

## Next

History, Liked videos, Playlists (with the "Save to playlist" dialog), profile settings (avatar, password, pause history), admin screens.
