const base = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '');

/** Turns a relative API path ("/media/...", "/api/stream/...") into a URL the browser can load. */
export function apiUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path;
  return `${base}${path}`;
}

export const API_BASE = base;
