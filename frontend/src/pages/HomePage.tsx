import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { feedApi } from '../api/endpoints';
import type { TrendingWindow } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import VideoCard from '../components/VideoCard';
import VideoGrid from '../components/VideoGrid';
import { nextPage } from '../lib/paging';

const WINDOWS: TrendingWindow[] = ['Day', 'Week', 'Month'];

export default function HomePage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const [window, setWindow] = useState<TrendingWindow>('Week');

  const tab = params.get('tab') === 'trending' ? 'trending' : 'recommended';
  const userKey = user?.id ?? 'anon';

  useEffect(() => {
    document.title = 'MiniTube';
  }, []);

  const recommended = useInfiniteQuery({
    queryKey: ['feed', 'recommended', userKey],
    queryFn: ({ pageParam }) => feedApi.recommended(pageParam),
    initialPageParam: 1,
    getNextPageParam: nextPage,
    enabled: tab === 'recommended'
  });

  const trending = useInfiniteQuery({
    queryKey: ['feed', 'trending', window],
    queryFn: ({ pageParam }) => feedApi.trending(window, pageParam),
    initialPageParam: 1,
    getNextPageParam: nextPage,
    enabled: tab === 'trending'
  });

  const resume = useQuery({
    queryKey: ['feed', 'continue', userKey],
    queryFn: feedApi.continueWatching,
    enabled: !!user && tab === 'recommended'
  });

  const setTab = (next: 'recommended' | 'trending') => setParams(next === 'trending' ? { tab: 'trending' } : {});

  return (
    <div>
      <div className="home__top">
        <div className="chips" role="group" aria-label={t('home.feed')}>
          <button className="chip" aria-pressed={tab === 'recommended'} onClick={() => setTab('recommended')}>
            {user ? t('home.forYou') : t('home.popular')}
          </button>
          <button className="chip" aria-pressed={tab === 'trending'} onClick={() => setTab('trending')}>
            {t('home.trending')}
          </button>
        </div>

        {tab === 'trending' && (
          <div className="chips" role="group" aria-label={t('home.period')}>
            {WINDOWS.map((w) => (
              <button key={w} className="chip" aria-pressed={window === w} onClick={() => setWindow(w)}>
                {t(`home.window${w}`)}
              </button>
            ))}
          </div>
        )}
      </div>

      {tab === 'recommended' && resume.data && resume.data.length > 0 && (
        <section aria-labelledby="continue-title">
          <h2 id="continue-title" className="row-title">
            {t('home.continueWatching')}
          </h2>
          <div className="hscroll">
            {resume.data.map((item) => (
              <VideoCard
                key={item.video.id}
                video={item.video}
                progress={item.video.durationSeconds > 0 ? item.positionSeconds / item.video.durationSeconds : undefined}
              />
            ))}
          </div>
        </section>
      )}

      <VideoGrid query={tab === 'trending' ? trending : recommended} />
    </div>
  );
}
