export type Visibility = 'Private' | 'Unlisted' | 'Public';
export type VideoStatus = 'PendingUpload' | 'Uploading' | 'Queued' | 'Processing' | 'Ready' | 'Failed';
export type ReactionType = 'Like' | 'Dislike';
export type TrendingWindow = 'Day' | 'Week' | 'Month';
export type SearchSort = 'Relevance' | 'Newest' | 'MostViewed' | 'MostLiked';
export type UploadDateFilter = 'Any' | 'Today' | 'Week' | 'Month' | 'Year';
export type DurationFilter = 'Any' | 'Short' | 'Medium' | 'Long';

export interface UserDto {
  id: string;
  userName: string;
  email: string;
  emailConfirmed: boolean;
  watchHistoryPaused: boolean;
  displayName: string;
  bio: string | null;
  avatarUrl: string | null;
  roles: string[];
  createdAt: string;
}

export interface PublicUser {
  id: string;
  userName: string;
  displayName: string;
  bio: string | null;
  avatarUrl: string | null;
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserDto;
}

export interface VideoOwner {
  id: string;
  userName: string;
  displayName: string;
  avatarUrl: string | null;
}

export interface Rendition {
  name: string;
  width: number;
  height: number;
  bitrateKbps: number;
}

export interface VideoDto {
  id: string;
  title: string;
  description: string | null;
  visibility: Visibility;
  status: VideoStatus;
  owner: VideoOwner;
  durationSeconds: number;
  width: number;
  height: number;
  thumbnailUrl: string | null;
  streamUrl: string | null;
  renditions: Rendition[];
  createdAt: string;
  publishedAt: string | null;
  viewCount: number;
  likeCount: number;
  myReaction: ReactionType | null;
  resumePositionSeconds: number | null;
  dislikeCount: number | null;
  sizeBytes: number | null;
  processingProgress: number | null;
  processingError: string | null;
}

export interface VideoListItem {
  id: string;
  title: string;
  thumbnailUrl: string | null;
  durationSeconds: number;
  visibility: Visibility;
  status: VideoStatus;
  processingProgress: number;
  viewCount: number;
  owner: VideoOwner;
  createdAt: string;
  publishedAt: string | null;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ReactionState {
  myReaction: ReactionType | null;
  likeCount: number;
}

export interface ViewResult {
  counted: boolean;
  viewCount: number;
}

export interface WatchHistoryItem {
  video: VideoListItem;
  positionSeconds: number;
  completed: boolean;
  watchedAt: string;
}

export interface ChannelResult {
  id: string;
  userName: string;
  displayName: string;
  avatarUrl: string | null;
  videoCount: number;
}

export interface Suggestion {
  type: 'video' | 'channel';
  text: string;
  id: string | null;
}

export interface VideoInput {
  title: string;
  description: string | null;
  visibility: Visibility;
}
