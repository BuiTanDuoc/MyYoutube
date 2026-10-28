import { Link } from 'react-router-dom';
import type { VideoListItem } from '../api/types';
import { useFormatters } from '../lib/format';
import Avatar from './Avatar';
import { useViewsText, VideoThumb } from './VideoCard';

interface Props {
  video: VideoListItem;
  variant?: 'large' | 'compact';
}

/** Horizontal card: search results (large) and the "related" sidebar (compact). */
export default function VideoRow({ video, variant = 'compact' }: Props) {
  const fmt = useFormatters();
  const viewsText = useViewsText();

  return (
    <article className={`vrow vrow--${variant}`}>
      <VideoThumb video={video} />
      <div>
        <h3 className="vrow__title">
          <Link to={`/watch/${video.id}`}>{video.title}</Link>
        </h3>
        {variant === 'large' ? (
          <>
            <p className="vrow__meta">
              {viewsText(video.viewCount)}
              {video.publishedAt && <> · {fmt.timeAgo(video.publishedAt)}</>}
            </p>
            <Link to={`/channel/${video.owner.id}`} className="vrow__channel">
              <Avatar name={video.owner.displayName} src={video.owner.avatarUrl} size={24} />
              {video.owner.displayName}
            </Link>
          </>
        ) : (
          <>
            <p className="vrow__meta">
              <Link to={`/channel/${video.owner.id}`}>{video.owner.displayName}</Link>
            </p>
            <p className="vrow__meta">
              {viewsText(video.viewCount)}
              {video.publishedAt && <> · {fmt.timeAgo(video.publishedAt)}</>}
            </p>
          </>
        )}
      </div>
    </article>
  );
}
