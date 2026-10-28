import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Clapperboard, Loader, ThumbsDown, ThumbsUp, TriangleAlert, VideoOff } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { videosApi } from '../api/endpoints';
import { parseError } from '../api/errors';
import type { ReactionType, VideoDto } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import Avatar from '../components/Avatar';
import EmptyState from '../components/EmptyState';
import Player from '../components/Player';
import Spinner from '../components/Spinner';
import { useToast } from '../components/Toast';
import VideoRow from '../components/VideoRow';
import { useFormatters } from '../lib/format';

const BUSY = new Set(['PendingUpload', 'Uploading', 'Queued', 'Processing']);

export default function WatchPage() {
  const { id = '' } = useParams();
  const { t } = useTranslation();
  const { user } = useAuth();
  const fmt = useFormatters();
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const toast = useToast();

  const queryKey = ['video', id, user?.id ?? 'anon'] as const;

  const { data: video, isPending, isError } = useQuery({
    queryKey,
    queryFn: () => videosApi.get(id),
    enabled: !!id,
    retry: false,
    // Owners watching their own upload see the progress live
    refetchInterval: (q) => (q.state.data && BUSY.has(q.state.data.status) ? 3000 : false)
  });

  const related = useQuery({
    queryKey: ['related', id],
    queryFn: () => videosApi.related(id),
    enabled: video?.status === 'Ready',
    staleTime: 5 * 60_000
  });

  const [autoplay, setAutoplay] = useState(() => localStorage.getItem('mt.autoplay') !== 'off');
  const [expanded, setExpanded] = useState(false);
  // Resume position is captured once per video: later refetches must not move the playhead
  const resumeFor = useRef<{ id: string; at: number } | null>(null);

  useEffect(() => {
    setExpanded(false);
  }, [id]);

  useEffect(() => {
    document.title = video ? `${video.title} - MiniTube` : 'MiniTube';
    return () => {
      document.title = 'MiniTube';
    };
  }, [video?.title]); // eslint-disable-line react-hooks/exhaustive-deps

  const react = useMutation({
    mutationFn: (type: ReactionType | null) => videosApi.react(id, type),
    onSuccess: (state) =>
      queryClient.setQueryData<VideoDto>(queryKey, (old) => (old ? { ...old, myReaction: state.myReaction, likeCount: state.likeCount } : old)),
    onError: (e) => toast.error(parseError(e).message)
  });

  if (isPending) return <Spinner />;

  if (isError || !video) {
    return (
      <EmptyState
        icon={<VideoOff size={56} />}
        title={t('video.notAvailable')}
        hint={t('video.notAvailableHint')}
        action={
          <Link to="/" className="btn btn--primary">
            {t('notFound.home')}
          </Link>
        }
      />
    );
  }

  const isOwner = user?.id === video.owner.id;
  if (resumeFor.current?.id !== video.id) resumeFor.current = { id: video.id, at: video.resumePositionSeconds ?? 0 };

  const toggleReaction = (type: ReactionType) => {
    if (!user) {
      navigate('/login', { state: { from: location.pathname } });
      return;
    }
    react.mutate(video.myReaction === type ? null : type);
  };

  const toggleAutoplay = (on: boolean) => {
    setAutoplay(on);
    localStorage.setItem('mt.autoplay', on ? 'on' : 'off');
  };

  const onEnded = () => {
    const next = related.data?.[0];
    if (autoplay && next) navigate(`/watch/${next.id}`);
  };

  const onViewCounted = (viewCount: number) =>
    queryClient.setQueryData<VideoDto>(queryKey, (old) => (old ? { ...old, viewCount } : old));

  const ready = video.status === 'Ready' && video.streamUrl;

  return (
    <div className="watch">
      <div>
        {ready ? (
          <Player key={video.id} video={video} startAt={resumeFor.current.at} onEnded={onEnded} onViewCounted={onViewCounted} />
        ) : (
          <ProcessingCard video={video} isOwner={isOwner} />
        )}

        <h1 className="watch__title">{video.title}</h1>

        <div className="watch__bar">
          <div className="watch__owner">
            <Link to={`/channel/${video.owner.id}`}>
              <Avatar name={video.owner.displayName} src={video.owner.avatarUrl} size={40} />
            </Link>
            <Link to={`/channel/${video.owner.id}`}>
              <strong>{video.owner.displayName}</strong>
            </Link>
          </div>

          <div className="watch__actions">
            {video.visibility !== 'Public' && <span className="badge">{t(`visibility.${video.visibility}`)}</span>}
            {isOwner && (
              <Link to="/studio" className="btn btn--sm">
                <Clapperboard size={16} /> {t('video.manage')}
              </Link>
            )}
            {ready && (
              <div className="seg" role="group" aria-label={t('video.reactions')}>
                <button aria-pressed={video.myReaction === 'Like'} onClick={() => toggleReaction('Like')} disabled={react.isPending}>
                  <ThumbsUp size={18} fill={video.myReaction === 'Like' ? 'currentColor' : 'none'} />
                  {fmt.compact(video.likeCount)}
                  <span className="sr-only">{t('video.like')}</span>
                </button>
                <button aria-pressed={video.myReaction === 'Dislike'} onClick={() => toggleReaction('Dislike')} disabled={react.isPending}>
                  <ThumbsDown size={18} fill={video.myReaction === 'Dislike' ? 'currentColor' : 'none'} />
                  <span className="sr-only">{t('video.dislike')}</span>
                </button>
              </div>
            )}
          </div>
        </div>

        <section className={`watch__desc ${expanded ? '' : 'is-clamped'}`}>
          <strong>
            {t('video.views', { count: video.viewCount, views: fmt.full(video.viewCount) })}
            {video.publishedAt && <> · {fmt.date(video.publishedAt)}</>}
          </strong>
          <p>{video.description || t('video.noDescription')}</p>
          {(video.description?.length ?? 0) > 160 || (video.description?.split('\n').length ?? 0) > 3 ? (
            <button onClick={() => setExpanded((e) => !e)}>{expanded ? t('video.showLess') : t('video.showMore')}</button>
          ) : null}
        </section>
      </div>

      <aside className="watch__side" aria-label={t('video.upNext')}>
        <div className="watch__side-head">
          <h2 className="row-title" style={{ margin: 0, fontSize: 16 }}>
            {t('video.upNext')}
          </h2>
          <label className="switch">
            <input type="checkbox" checked={autoplay} onChange={(e) => toggleAutoplay(e.target.checked)} />
            {t('video.autoplay')}
          </label>
        </div>
        {related.isPending && ready && <Spinner />}
        {related.data?.map((v) => <VideoRow key={v.id} video={v} />)}
      </aside>
    </div>
  );
}

function ProcessingCard({ video, isOwner }: { video: VideoDto; isOwner: boolean }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const toast = useToast();

  const reprocess = useMutation({
    mutationFn: () => videosApi.reprocess(video.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['video', video.id] }),
    onError: (e) => toast.error(parseError(e).message)
  });

  if (video.status === 'Failed') {
    return (
      <div className="processing" role="alert">
        <TriangleAlert size={40} />
        <strong>{t('video.failed')}</strong>
        {isOwner && video.processingError && <p className="muted">{video.processingError}</p>}
        {isOwner && (
          <button className="btn btn--primary" onClick={() => reprocess.mutate()} disabled={reprocess.isPending}>
            {t('video.retryProcessing')}
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="processing" role="status">
      <Loader size={40} />
      <strong>{video.status === 'PendingUpload' ? t('video.waitingUpload') : t('video.processing')}</strong>
      <p className="muted">{t('video.processingHint')}</p>
      {video.status === 'Processing' && (
        <div className="progress" role="progressbar" aria-valuenow={video.processingProgress ?? 0} aria-valuemin={0} aria-valuemax={100}>
          <span style={{ width: `${video.processingProgress ?? 0}%` }} />
        </div>
      )}
    </div>
  );
}
