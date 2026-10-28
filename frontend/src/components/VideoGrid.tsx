import { InfiniteData, UseInfiniteQueryResult } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Film } from 'lucide-react';
import type { Paged, VideoListItem } from '../api/types';
import { parseError } from '../api/errors';
import { useInfiniteSentinel } from '../hooks/useInfiniteSentinel';
import EmptyState from './EmptyState';
import VideoCard, { VideoCardSkeleton } from './VideoCard';

interface Props {
  query: UseInfiniteQueryResult<InfiniteData<Paged<VideoListItem>>, Error>;
  emptyTitle?: string;
  emptyHint?: string;
}

/** Responsive grid with infinite scrolling for any paged list of videos. */
export default function VideoGrid({ query, emptyTitle, emptyHint }: Props) {
  const { t } = useTranslation();
  const { data, isPending, isError, error, hasNextPage, isFetchingNextPage, fetchNextPage, refetch } = query;

  const sentinel = useInfiniteSentinel(() => {
    if (hasNextPage && !isFetchingNextPage) void fetchNextPage();
  }, !!hasNextPage);

  if (isPending) {
    return (
      <div className="video-grid">
        {Array.from({ length: 12 }, (_, i) => (
          <VideoCardSkeleton key={i} />
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <EmptyState
        title={t('errors.loadFailed')}
        hint={parseError(error).message}
        action={
          <button className="btn btn--primary" onClick={() => void refetch()}>
            {t('common.retry')}
          </button>
        }
      />
    );
  }

  const items = data.pages.flatMap((p) => p.items);
  if (items.length === 0) {
    return <EmptyState icon={<Film size={48} />} title={emptyTitle ?? t('home.empty')} hint={emptyHint ?? t('home.emptyHint')} />;
  }

  return (
    <>
      <div className="video-grid">
        {items.map((v) => (
          <VideoCard key={v.id} video={v} />
        ))}
        {isFetchingNextPage && Array.from({ length: 4 }, (_, i) => <VideoCardSkeleton key={`s${i}`} />)}
      </div>
      <div ref={sentinel} className="load-more" />
    </>
  );
}
