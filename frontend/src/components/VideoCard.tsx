import { Link } from 'react-router-dom';
import { Film } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { VideoListItem } from '../api/types';
import { formatDuration, useFormatters } from '../lib/format';
import { apiUrl } from '../lib/url';
import Avatar from './Avatar';

export function VideoThumb({ video, progress }: { video: VideoListItem; progress?: number }) {
  return (
    <Link to={`/watch/${video.id}`} className="vthumb" tabIndex={-1} aria-hidden="true">
      {video.thumbnailUrl ? (
        <img src={apiUrl(video.thumbnailUrl)} alt="" loading="lazy" />
      ) : (
        <span className="vthumb__placeholder">
          <Film size={36} />
        </span>
      )}
      {video.durationSeconds > 0 && <span className="vthumb__duration">{formatDuration(video.durationSeconds)}</span>}
      {progress !== undefined && progress > 0 && (
        <span className="vthumb__bar">
          <span style={{ width: `${Math.min(100, progress * 100)}%` }} />
        </span>
      )}
    </Link>
  );
}

export function useViewsText() {
  const { t } = useTranslation();
  const fmt = useFormatters();
  return (count: number) => t('video.views', { count, views: fmt.compact(count) });
}

interface Props {
  video: VideoListItem;
  /** 0..1, draws the red "watched so far" bar (continue watching). */
  progress?: number;
}

export default function VideoCard({ video, progress }: Props) {
  const fmt = useFormatters();
  const viewsText = useViewsText();

  return (
    <article className="vcard">
      <VideoThumb video={video} progress={progress} />
      <div className="vcard__body">
        <Link to={`/channel/${video.owner.id}`} aria-label={video.owner.displayName}>
          <Avatar name={video.owner.displayName} src={video.owner.avatarUrl} />
        </Link>
        <div>
          <h3 className="vcard__title">
            <Link to={`/watch/${video.id}`}>{video.title}</Link>
          </h3>
          <p className="vcard__meta">
            <Link to={`/channel/${video.owner.id}`}>{video.owner.displayName}</Link>
          </p>
          <p className="vcard__meta">
            {viewsText(video.viewCount)}
            {video.publishedAt && <> · {fmt.timeAgo(video.publishedAt)}</>}
          </p>
        </div>
      </div>
    </article>
  );
}

export function VideoCardSkeleton() {
  return (
    <div className="vcard vcard--skeleton" aria-hidden="true">
      <div className="vthumb skeleton" />
      <div className="vcard__body">
        <div className="avatar skeleton" style={{ width: 36, height: 36 }} />
        <div style={{ flex: 1, display: 'grid', gap: 8 }}>
          <div className="sk-line skeleton" style={{ width: '90%' }} />
          <div className="sk-line skeleton" style={{ width: '60%' }} />
        </div>
      </div>
    </div>
  );
}
