import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { applySession, endSession, onSessionChange, refreshSession } from '../api/client';
import { authApi, usersApi } from '../api/endpoints';
import { tokens } from '../api/tokens';
import type { UserDto } from '../api/types';

interface AuthContextValue {
  user: UserDto | null;
  /** False while the stored session is being restored on startup. */
  ready: boolean;
  login: (identifier: string, password: string) => Promise<UserDto>;
  register: (input: { email: string; userName: string; displayName: string; password: string }) => Promise<UserDto>;
  logout: () => Promise<void>;
  /** Re-reads /users/me (after a profile change). */
  reloadUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [ready, setReady] = useState(false);
  const queryClient = useQueryClient();

  // Background refreshes and forced sign-outs (e.g. refresh token rejected) update the UI automatically
  useEffect(() => onSessionChange(setUser), []);

  // Restore the session before rendering routes, so the very first requests already carry a token
  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (tokens.refresh) await refreshSession();
      if (!cancelled) setReady(true);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const login = useCallback(
    async (identifier: string, password: string) => {
      const res = await authApi.login({ identifier, password });
      applySession(res);
      await queryClient.invalidateQueries(); // personalised data (feed, reactions...) must be refetched
      return res.user;
    },
    [queryClient]
  );

  const register = useCallback(
    async (input: { email: string; userName: string; displayName: string; password: string }) => {
      const res = await authApi.register(input);
      applySession(res);
      await queryClient.invalidateQueries();
      return res.user;
    },
    [queryClient]
  );

  const logout = useCallback(async () => {
    const refreshToken = tokens.refresh;
    if (refreshToken) await authApi.logout(refreshToken).catch(() => undefined);
    endSession();
    queryClient.clear();
  }, [queryClient]);

  const reloadUser = useCallback(async () => {
    setUser(await usersApi.me());
  }, []);

  const value = useMemo(
    () => ({ user, ready, login, register, logout, reloadUser }),
    [user, ready, login, register, logout, reloadUser]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}
