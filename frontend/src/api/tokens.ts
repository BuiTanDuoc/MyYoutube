const REFRESH_KEY = 'mt.refresh';

/**
 * The access token lives in memory only. The refresh token is kept in localStorage so the session
 * survives a reload (trade-off: readable by injected scripts, so keep the app XSS-clean; an httpOnly
 * cookie would be the hardening step).
 */
export const tokens = {
  access: null as string | null,
  accessExpiresAt: 0,

  get refresh(): string | null {
    try {
      return localStorage.getItem(REFRESH_KEY);
    } catch {
      return null;
    }
  },

  set(accessToken: string, accessExpiresAtIso: string, refreshToken: string) {
    this.access = accessToken;
    this.accessExpiresAt = Date.parse(accessExpiresAtIso);
    try {
      localStorage.setItem(REFRESH_KEY, refreshToken);
    } catch {
      /* private mode: the session just will not survive a reload */
    }
  },

  clear() {
    this.access = null;
    this.accessExpiresAt = 0;
    try {
      localStorage.removeItem(REFRESH_KEY);
    } catch {
      /* ignore */
    }
  }
};
