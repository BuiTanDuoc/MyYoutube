import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import i18n from '../i18n';
import { API_BASE } from '../lib/url';
import { tokens } from './tokens';
import type { AuthResponse, UserDto } from './types';

export const api = axios.create({ baseURL: API_BASE });

const currentLanguage = () => (i18n.language?.startsWith('en') ? 'en' : 'vi');

// ---------------------------------------------------------------- session events

type SessionListener = (user: UserDto | null) => void;
const listeners = new Set<SessionListener>();

/** AuthContext subscribes here to learn about background refreshes and forced sign-outs. */
export function onSessionChange(listener: SessionListener) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

const emit = (user: UserDto | null) => listeners.forEach((l) => l(user));

let refreshTimer: number | undefined;

function scheduleRefresh() {
  window.clearTimeout(refreshTimer);
  const delay = Math.max(5_000, tokens.accessExpiresAt - Date.now() - 60_000);
  refreshTimer = window.setTimeout(() => void refreshSession(), delay);
}

export function applySession(res: AuthResponse) {
  tokens.set(res.accessToken, res.accessTokenExpiresAt, res.refreshToken);
  scheduleRefresh();
  emit(res.user);
}

export function endSession() {
  window.clearTimeout(refreshTimer);
  tokens.clear();
  emit(null);
}

// ---------------------------------------------------------------- refresh (single flight)

let inflight: Promise<AuthResponse | null> | null = null;

async function callRefresh(): Promise<AuthResponse | null> {
  const refreshToken = tokens.refresh; // read late: another tab may have rotated it while we waited for the lock
  if (!refreshToken) return null;

  try {
    const { data } = await axios.post<AuthResponse>(
      `${API_BASE}/api/auth/refresh`,
      { refreshToken },
      { headers: { 'Accept-Language': currentLanguage() } }
    );
    applySession(data);
    return data;
  } catch (e) {
    const status = (e as AxiosError).response?.status;
    // Only a real rejection ends the session; a network blip must not sign the user out
    if (status === 400 || status === 401 || status === 403) endSession();
    return null;
  }
}

/**
 * Exchanges the refresh token for a new pair. Safe to call from anywhere at any time:
 * concurrent calls in this tab share one request, and a Web Lock serialises tabs
 * (the server treats a reused refresh token as theft and signs every device out).
 */
export function refreshSession(): Promise<AuthResponse | null> {
  if (inflight) return inflight;

  const run = () => callRefresh();
  inflight = (navigator.locks ? navigator.locks.request('mt-refresh', run) : run()).finally(() => {
    inflight = null;
  });
  return inflight;
}

// ---------------------------------------------------------------- interceptors

api.interceptors.request.use(async (config) => {
  const isAuthCall = config.url?.startsWith('/api/auth/');

  // Renew shortly BEFORE expiry: optional-auth endpoints (the watch page...) never answer 401,
  // they would silently treat an expired token as "anonymous".
  if (!isAuthCall && tokens.refresh && tokens.accessExpiresAt - Date.now() < 30_000) {
    await refreshSession();
  }

  if (tokens.access) config.headers.Authorization = `Bearer ${tokens.access}`;
  config.headers['Accept-Language'] = currentLanguage();
  return config;
});

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetriableConfig | undefined;
    const isAuthCall = config?.url?.startsWith('/api/auth/');

    if (error.response?.status === 401 && config && !config._retried && !isAuthCall && tokens.refresh) {
      config._retried = true;
      const session = await refreshSession();
      if (session) {
        config.headers.Authorization = `Bearer ${session.accessToken}`;
        return api(config);
      }
    }
    return Promise.reject(error);
  }
);
