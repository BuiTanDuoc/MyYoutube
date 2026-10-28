import { useEffect } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { usersApi, videosApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import Avatar from '../components/Avatar';
import Spinner from '../components/Spinner';
import VideoGrid from '../components/VideoGrid';
import { useFormatters } from '../lib/format';
import { nextPage } from '../lib/paging';
import NotFoundPage from './NotFoundPage';

export default function ChannelPage() {
  const { id = '' } = useParams();
  const { t } = useTranslation();
  const { user } = useAuth();
  const fmt = useFormatters();

  const profile = useQuery({ queryKey: ['user', id], queryFn: () => usersApi.publicProfile(id), retry: false, enabled: !!id });

  const videos = useInfiniteQuery({
    queryKey: ['channel-videos', id],
    queryFn: ({ pageParam }) => videosApi.list(pageParam, id),
    initialPageParam: 1,
    getNextPageParam: nextPage,
    enabled: !!id
  });

  useEffect(() => {
    document.title = profile.data ? `${profile.data.displayName} - MiniTube` : 'MiniTube';
  }, [profile.data]);

  if (profile.isPending) return <Spinner />;
  if (profile.isError || !profile.data) return <NotFoundPage />;

  const p = profile.data;
  const total = videos.data?.pages[0]?.totalCount;

  return (
    <div className="channel">
      <header className="channel__head">
        <Avatar name={p.displayName} src={p.avatarUrl} size={96} />
        <div>
          <h1>{p.displayName}</h1>
          <p className="muted">
            @{p.userName}
            {total !== undefined && <> · {t('search.channelVideos', { count: total })}</>} · {t('channel.joined', { date: fmt.date(p.createdAt) })}
          </p>
          {p.bio && <p className="channel__bio">{p.bio}</p>}
          {user?.id === p.id && (
            <p style={{ marginTop: 12 }}>
              <Link to="/studio" className="btn btn--sm">
                {t('nav.studio')}
              </Link>
            </p>
          )}
        </div>
      </header>

      <VideoGrid query={videos} emptyTitle={t('channel.noVideos')} emptyHint="" />
    </div>
  );
}
