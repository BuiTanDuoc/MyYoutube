import axios, { AxiosAdapter, AxiosError, InternalAxiosRequestConfig } from 'axios';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api, onSessionChange, refreshSession } from './client';
import { tokens } from './tokens';
import type { AuthResponse, UserDto } from './types';

const user = { id: 'u1', displayName: 'Test' } as UserDto;

function session(n: number, expiresInMs = 15 * 60_000): AuthResponse {
  return {
    accessToken: `access-${n}`,
    accessTokenExpiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    refreshToken: `refresh-${n}`,
    refreshTokenExpiresAt: new Date(Date.now() + 7 * 864e5).toISOString(),
    user
  };
}

function httpError(config: InternalAxiosRequestConfig, status: number) {
  return new AxiosError('fail', String(status), config, null, {
    status,
    statusText: '',
    data: {},
    headers: {},
    config
  });
}

describe('api client', () => {
  beforeEach(() => {
    localStorage.clear();
    tokens.clear();
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('shares ONE refresh request between concurrent callers', async () => {
    tokens.set('old', new Date(Date.now() - 1000).toISOString(), 'refresh-0');
    const post = vi.spyOn(axios, 'post').mockResolvedValue({ data: session(1) });

    const [a, b, c] = await Promise.all([refreshSession(), refreshSession(), refreshSession()]);

    expect(post).toHaveBeenCalledTimes(1);
    expect(a?.accessToken).toBe('access-1');
    expect(b).toBe(a);
    expect(c).toBe(a);
    expect(tokens.refresh).toBe('refresh-1'); // rotated token is stored
  });

  it('renews an access token that is about to expire BEFORE sending the request', async () => {
    tokens.set('almost-expired', new Date(Date.now() + 5_000).toISOString(), 'refresh-0');
    vi.spyOn(axios, 'post').mockResolvedValue({ data: session(1) });

    let sentWith = '';
    const adapter: AxiosAdapter = async (config) => {
      sentWith = String(config.headers.Authorization);
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    await api.get('/api/videos/x', { adapter });
    expect(sentWith).toBe('Bearer access-1');
  });

  it('retries once with a fresh token after a 401', async () => {
    tokens.set('stale', new Date(Date.now() + 10 * 60_000).toISOString(), 'refresh-0');
    vi.spyOn(axios, 'post').mockResolvedValue({ data: session(2) });

    const seen: string[] = [];
    const adapter: AxiosAdapter = async (config) => {
      seen.push(String(config.headers.Authorization));
      if (seen.length === 1) throw httpError(config, 401);
      return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config };
    };

    const res = await api.get('/api/users/me', { adapter });
    expect(res.data).toEqual({ ok: true });
    expect(seen).toEqual(['Bearer stale', 'Bearer access-2']);
  });

  it('signs the user out when the refresh token is rejected', async () => {
    tokens.set('old', new Date(Date.now() - 1000).toISOString(), 'refresh-0');
    vi.spyOn(axios, 'post').mockRejectedValue(httpError({} as InternalAxiosRequestConfig, 401));

    const events: (UserDto | null)[] = [];
    const off = onSessionChange((u) => events.push(u));

    expect(await refreshSession()).toBeNull();
    expect(events).toEqual([null]);
    expect(tokens.refresh).toBeNull();
    off();
  });

  it('keeps the session when the refresh fails because of a network error', async () => {
    tokens.set('old', new Date(Date.now() - 1000).toISOString(), 'refresh-0');
    vi.spyOn(axios, 'post').mockRejectedValue(new AxiosError('Network Error'));

    expect(await refreshSession()).toBeNull();
    expect(tokens.refresh).toBe('refresh-0'); // not logged out by a connectivity blip
  });

  it('never tries to refresh for auth endpoints (no infinite loops)', async () => {
    tokens.set('stale', new Date(Date.now() + 10 * 60_000).toISOString(), 'refresh-0');
    const post = vi.spyOn(axios, 'post');

    const adapter: AxiosAdapter = async (config) => {
      throw httpError(config, 401);
    };

    await expect(api.post('/api/auth/login', {}, { adapter })).rejects.toBeTruthy();
    expect(post).not.toHaveBeenCalled();
  });
});
