import { api } from './client';
import { tokens } from './tokens';
import { apiUrl } from '../lib/url';
import type {
  AuthResponse,
  ChannelResult,
  DurationFilter,
  Paged,
  PublicUser,
  ReactionState,
  ReactionType,
  SearchSort,
  Suggestion,
  TrendingWindow,
  UploadDateFilter,
  UserDto,
  VideoDto,
  VideoInput,
  VideoListItem,
  VideoStatus,
  ViewResult,
  WatchHistoryItem
} from './types';

export const authApi = {
  register: (body: { email: string; userName: string; displayName: string; password: string }) =>
    api.post<AuthResponse>('/api/auth/register', body).then((r) => r.data),
  login: (body: { identifier: string; password: string }) =>
    api.post<AuthResponse>('/api/auth/login', body).then((r) => r.data),
  logout: (refreshToken: string) => api.post('/api/auth/logout', { refreshToken }),
  confirmEmail: (userId: string, token: string) => api.post('/api/auth/confirm-email', { userId, token }),
  resendConfirmation: (email: string) => api.post('/api/auth/resend-confirmation', { email }),
  forgotPassword: (email: string) => api.post('/api/auth/forgot-password', { email }),
  resetPassword: (body: { email: string; token: string; newPassword: string }) =>
    api.post('/api/auth/reset-password', body)
};

export const usersApi = {
  me: () => api.get<UserDto>('/api/users/me').then((r) => r.data),
  publicProfile: (id: string) => api.get<PublicUser>(`/api/users/${id}`).then((r) => r.data)
};

export const videosApi = {
  create: (body: VideoInput) => api.post<VideoDto>('/api/videos', body).then((r) => r.data),

  /** Raw file as the request body (not multipart); the API streams it to disk. */
  upload: (id: string, file: File, onProgress: (fraction: number) => void, signal?: AbortSignal) =>
    api
      .put<VideoDto>(`/api/videos/${id}/source`, file, {
        params: { fileName: file.name },
        headers: { 'Content-Type': 'application/octet-stream' },
        onUploadProgress: (e) => onProgress(e.total ? e.loaded / e.total : 0),
        timeout: 0,
        maxBodyLength: Infinity,
        signal
      })
      .then((r) => r.data),

  get: (id: string) => api.get<VideoDto>(`/api/videos/${id}`).then((r) => r.data),
  list: (page: number, ownerId?: string) =>
    api.get<Paged<VideoListItem>>('/api/videos', { params: { page, pageSize: 24, ownerId } }).then((r) => r.data),
  mine: (page: number, status?: VideoStatus) =>
    api.get<Paged<VideoListItem>>('/api/videos/mine', { params: { page, pageSize: 20, status } }).then((r) => r.data),
  update: (id: string, body: VideoInput) => api.put<VideoDto>(`/api/videos/${id}`, body).then((r) => r.data),
  remove: (id: string) => api.delete(`/api/videos/${id}`),
  reprocess: (id: string) => api.post<VideoDto>(`/api/videos/${id}/reprocess`).then((r) => r.data),

  related: (id: string) =>
    api.get<VideoListItem[]>(`/api/videos/${id}/related`, { params: { limit: 20 } }).then((r) => r.data),

  view: (id: string) => api.post<ViewResult>(`/api/videos/${id}/views`).then((r) => r.data),
  react: (id: string, type: ReactionType | null) =>
    api.put<ReactionState>(`/api/videos/${id}/reaction`, { type }).then((r) => r.data)
};

/**
 * Progress heartbeat. Uses fetch with keepalive so it still goes out while the page is being closed
 * (navigator.sendBeacon cannot send an Authorization header).
 */
export function sendProgress(videoId: string, positionSeconds: number) {
  const token = tokens.access;
  if (!token) return;

  fetch(apiUrl(`/api/videos/${videoId}/progress`), {
    method: 'PUT',
    keepalive: true,
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ positionSeconds })
  }).catch(() => undefined);
}

export const feedApi = {
  recommended: (page: number) =>
    api.get<Paged<VideoListItem>>('/api/feed/recommended', { params: { page, pageSize: 24 } }).then((r) => r.data),
  trending: (window: TrendingWindow, page: number) =>
    api.get<Paged<VideoListItem>>('/api/feed/trending', { params: { window, page, pageSize: 24 } }).then((r) => r.data),
  continueWatching: () =>
    api.get<WatchHistoryItem[]>('/api/feed/continue-watching', { params: { limit: 12 } }).then((r) => r.data)
};

export interface VideoSearchParams {
  q: string;
  sort: SearchSort;
  uploadDate: UploadDateFilter;
  duration: DurationFilter;
}

export const searchApi = {
  videos: (p: VideoSearchParams, page: number) =>
    api
      .get<Paged<VideoListItem>>('/api/search/videos', {
        params: { q: p.q || undefined, sort: p.sort, uploadDate: p.uploadDate, duration: p.duration, page, pageSize: 20 }
      })
      .then((r) => r.data),
  channels: (q: string) =>
    api.get<Paged<ChannelResult>>('/api/search/channels', { params: { q, pageSize: 3 } }).then((r) => r.data),
  suggestions: (q: string, signal?: AbortSignal) =>
    api.get<Suggestion[]>('/api/search/suggestions', { params: { q }, signal }).then((r) => r.data)
};
