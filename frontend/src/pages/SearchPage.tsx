import { useEffect } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { SearchX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { searchApi } from '../api/endpoints';
import { parseError } from '../api/errors';
import type { DurationFilter, SearchSort, UploadDateFilter } from '../api/types';
import Avatar from '../components/Avatar';
import EmptyState from '../components/EmptyState';
import Spinner from '../components/Spinner';
import VideoRow from '../components/VideoRow';
import { useInfiniteSentinel } from '../hooks/useInfiniteSentinel';
import { nextPage } from '../lib/paging';

const SORTS: SearchSort[] = ['Relevance', 'Newest', 'MostViewed', 'MostLiked'];
const DATES: UploadDateFilter[] = ['Any', 'Today', 'Week', 'Month', 'Year'];
const DURATIONS: DurationFilter[] = ['Any', 'Short', 'Medium', 'Long'];

const pick = <T extends string>(value: string | null, allowed: readonly T[], fallback: T): T =>
  allowed.includes(value as T) ? (value as T) : fallback;

export default function SearchPage() {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();

  const q = (params.get('q') ?? '').trim();
  const sort = pick(params.get('sort'), SORTS, 'Relevance');
  const uploadDate = pick(params.get('uploadDate'), DATES, 'Any');
  const duration = pick(params.get('duration'), DURATIONS, 'Any');

  useEffect(() => {
    document.title = q ? `${q} - MiniTube` : 'MiniTube';
  }, [q]);

  const results = useInfiniteQuery({
    queryKey: ['search', q, sort, uploadDate, duration],
    queryFn: ({ pageParam }) => searchApi.videos({ q, sort, uploadDate, duration }, pageParam),
    initialPageParam: 1,
    getNextPageParam: nextPage
  });

  const channels = useQuery({
    queryKey: ['search-channels', q],
    queryFn: () => searchApi.channels(q),
    enabled: q.length > 0,
    staleTime: 60_000
  });

  const sentinel = useInfiniteSentinel(() => {
    if (results.hasNextPage && !results.isFetchingNextPage) void results.fetchNextPage();
  }, !!results.hasNextPage);

  const update = (key: string, value: string, defaultValue: string) => {
    const next = new URLSearchParams(params);
    if (value === defaultValue) next.delete(key);
    else next.set(key, value);
    setParams(next, { replace: true });
  };

  const items = results.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <div className="search">
      <div className="search__filters">
        <div className="field">
          <label htmlFor="f-sort">{t('search.sort')}</label>
          <select id="f-sort" className="select" value={sort} onChange={(e) => update('sort', e.target.value, 'Relevance')}>
            {SORTS.map((s) => (
              <option key={s} value={s}>
                {t(`search.sort${s}`)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="f-date">{t('search.uploadDate')}</label>
          <select id="f-date" className="select" value={uploadDate} onChange={(e) => update('uploadDate', e.target.value, 'Any')}>
            {DATES.map((d) => (
              <option key={d} value={d}>
                {t(`search.date${d}`)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="f-duration">{t('search.duration')}</label>
          <select id="f-duration" className="select" value={duration} onChange={(e) => update('duration', e.target.value, 'Any')}>
            {DURATIONS.map((d) => (
              <option key={d} value={d}>
                {t(`search.duration${d}`)}
              </option>
            ))}
          </select>
        </div>
      </div>

      {channels.data && channels.data.items.length > 0 && (
        <section aria-label={t('search.channels')} style={{ marginBottom: 16 }}>
          {channels.data.items.map((c) => (
            <Link key={c.id} to={`/channel/${c.id}`} className="channel-row">
              <Avatar name={c.displayName} src={c.avatarUrl} size={72} />
              <div>
                <strong>{c.displayName}</strong>
                <p className="muted">
                  @{c.userName} · {t('search.channelVideos', { count: c.videoCount })}
                </p>
              </div>
            </Link>
          ))}
        </section>
      )}

      {results.isPending && <Spinner />}

      {results.isError && (
        <EmptyState
          title={t('errors.loadFailed')}
          hint={parseError(results.error).message}
          action={
            <button className="btn btn--primary" onClick={() => void results.refetch()}>
              {t('common.retry')}
            </button>
          }
        />
      )}

      {results.isSuccess && items.length === 0 && (
        <EmptyState icon={<SearchX size={56} />} title={t('search.noResults')} hint={t('search.noResultsHint')} />
      )}

      {items.length > 0 && (
        <>
          <div className="search__list">
            {items.map((v) => (
              <VideoRow key={v.id} video={v} variant="large" />
            ))}
          </div>
          <div ref={sentinel} className="load-more">
            {results.isFetchingNextPage && <Spinner />}
          </div>
        </>
      )}
    </div>
  );
}
