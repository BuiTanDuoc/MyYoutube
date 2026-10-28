import { FormEvent, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Clapperboard, Pencil, RotateCcw, Trash2, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { videosApi } from '../api/endpoints';
import { parseError } from '../api/errors';
import type { VideoListItem, VideoStatus, Visibility } from '../api/types';
import Dialog from '../components/Dialog';
import EmptyState from '../components/EmptyState';
import Spinner from '../components/Spinner';
import { useToast } from '../components/Toast';
import { VideoThumb } from '../components/VideoCard';
import { useViewsText } from '../components/VideoCard';
import { useInfiniteSentinel } from '../hooks/useInfiniteSentinel';
import { useFormatters } from '../lib/format';
import { nextPage } from '../lib/paging';

const BUSY: VideoStatus[] = ['Uploading', 'Queued', 'Processing'];

function statusClass(status: VideoStatus) {
  if (status === 'Ready') return 'badge badge--ok';
  if (status === 'Failed') return 'badge badge--bad';
  if (BUSY.includes(status)) return 'badge badge--busy';
  return 'badge';
}

export default function StudioPage() {
  const { t } = useTranslation();
  const fmt = useFormatters();
  const viewsText = useViewsText();
  const queryClient = useQueryClient();
  const toast = useToast();

  const [editId, setEditId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<VideoListItem | null>(null);

  useEffect(() => {
    document.title = `${t('studio.title')} - MiniTube`;
  }, [t]);

  const list = useInfiniteQuery({
    queryKey: ['mine'],
    queryFn: ({ pageParam }) => videosApi.mine(pageParam),
    initialPageParam: 1,
    getNextPageParam: nextPage,
    // Keep conversions up to date while something is being processed
    refetchInterval: (q) => (q.state.data?.pages.some((p) => p.items.some((v) => BUSY.includes(v.status))) ? 4000 : false)
  });

  const sentinel = useInfiniteSentinel(() => {
    if (list.hasNextPage && !list.isFetchingNextPage) void list.fetchNextPage();
  }, !!list.hasNextPage);

  const remove = useMutation({
    mutationFn: (id: string) => videosApi.remove(id),
    onSuccess: () => {
      toast.success(t('studio.deleted'));
      setDeleting(null);
      void queryClient.invalidateQueries({ queryKey: ['mine'] });
    },
    onError: (e) => toast.error(parseError(e).message)
  });

  const reprocess = useMutation({
    mutationFn: (id: string) => videosApi.reprocess(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['mine'] }),
    onError: (e) => toast.error(parseError(e).message)
  });

  const items = list.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <div className="studio">
      <div className="studio__head">
        <h1 className="page-title" style={{ margin: 0 }}>
          {t('studio.title')}
        </h1>
        <Link to="/upload" className="btn btn--primary">
          <Upload size={18} /> {t('nav.upload')}
        </Link>
      </div>

      {list.isPending && <Spinner />}

      {list.isError && (
        <EmptyState
          title={t('errors.loadFailed')}
          hint={parseError(list.error).message}
          action={
            <button className="btn btn--primary" onClick={() => void list.refetch()}>
              {t('common.retry')}
            </button>
          }
        />
      )}

      {list.isSuccess && items.length === 0 && (
        <EmptyState
          icon={<Clapperboard size={56} />}
          title={t('studio.empty')}
          hint={t('studio.emptyHint')}
          action={
            <Link to="/upload" className="btn btn--primary">
              {t('nav.upload')}
            </Link>
          }
        />
      )}

      <div className="studio__list">
        {items.map((v) => (
          <article key={v.id} className="studio-row">
            <VideoThumb video={v} />
            <div>
              <div className="studio-row__title">
                {v.status === 'Ready' ? <Link to={`/watch/${v.id}`}>{v.title}</Link> : v.title}
              </div>
              <div className="studio-row__meta">
                <span className={statusClass(v.status)}>{t(`status.${v.status}`)}</span>
                <span className="badge">{t(`visibility.${v.visibility}`)}</span>
                {v.status === 'Ready' && <span>{viewsText(v.viewCount)}</span>}
                <span>{fmt.date(v.createdAt)}</span>
              </div>
              {v.status === 'Processing' && (
                <div className="progress" role="progressbar" aria-valuenow={v.processingProgress} aria-valuemin={0} aria-valuemax={100}>
                  <span style={{ width: `${v.processingProgress}%` }} />
                </div>
              )}
            </div>
            <div className="studio-row__actions">
              <button className="btn btn--sm" onClick={() => setEditId(v.id)}>
                <Pencil size={15} /> {t('common.edit')}
              </button>
              {v.status === 'Failed' && (
                <button className="btn btn--sm" onClick={() => reprocess.mutate(v.id)} disabled={reprocess.isPending}>
                  <RotateCcw size={15} /> {t('studio.reprocess')}
                </button>
              )}
              <button className="btn btn--sm" onClick={() => setDeleting(v)} disabled={v.status === 'Uploading' || v.status === 'Processing'}>
                <Trash2 size={15} /> {t('common.delete')}
              </button>
            </div>
          </article>
        ))}
      </div>
      <div ref={sentinel} className="load-more">
        {list.isFetchingNextPage && <Spinner />}
      </div>

      <Dialog open={editId !== null} title={t('studio.editTitle')} onClose={() => setEditId(null)}>
        {editId && <EditForm videoId={editId} onDone={() => setEditId(null)} />}
      </Dialog>

      <Dialog
        open={deleting !== null}
        title={t('studio.deleteTitle')}
        onClose={() => setDeleting(null)}
        footer={
          <>
            <button className="btn" onClick={() => setDeleting(null)}>
              {t('common.cancel')}
            </button>
            <button className="btn btn--danger" onClick={() => deleting && remove.mutate(deleting.id)} disabled={remove.isPending}>
              {t('common.delete')}
            </button>
          </>
        }
      >
        <p>{t('studio.deleteConfirm', { title: deleting?.title ?? '' })}</p>
      </Dialog>
    </div>
  );
}

function EditForm({ videoId, onDone }: { videoId: string; onDone: () => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const toast = useToast();

  const { data, isPending, isError } = useQuery({ queryKey: ['video-edit', videoId], queryFn: () => videosApi.get(videoId), gcTime: 0 });

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [visibility, setVisibility] = useState<Visibility>('Private');

  useEffect(() => {
    if (!data) return;
    setTitle(data.title);
    setDescription(data.description ?? '');
    setVisibility(data.visibility);
  }, [data]);

  const save = useMutation({
    mutationFn: () => videosApi.update(videoId, { title: title.trim(), description: description.trim() || null, visibility }),
    onSuccess: () => {
      toast.success(t('studio.saved'));
      void queryClient.invalidateQueries({ queryKey: ['mine'] });
      void queryClient.invalidateQueries({ queryKey: ['video', videoId] });
      onDone();
    },
    onError: (e) => toast.error(parseError(e).message)
  });

  if (isPending) return <Spinner />;
  if (isError) return <p className="field__error">{t('errors.loadFailed')}</p>;

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (title.trim()) save.mutate();
  };

  return (
    <form onSubmit={submit}>
      <div className="field">
        <label htmlFor="edit-title">{t('upload.videoTitle')}</label>
        <input id="edit-title" className="input" value={title} maxLength={200} onChange={(e) => setTitle(e.target.value)} required />
      </div>
      <div className="field">
        <label htmlFor="edit-desc">{t('upload.description')}</label>
        <textarea id="edit-desc" className="textarea" value={description} maxLength={5000} onChange={(e) => setDescription(e.target.value)} />
      </div>
      <div className="field">
        <label htmlFor="edit-vis">{t('upload.visibility')}</label>
        <select id="edit-vis" className="select" value={visibility} onChange={(e) => setVisibility(e.target.value as Visibility)}>
          <option value="Private">{t('visibility.Private')} - {t('visibility.PrivateHint')}</option>
          <option value="Unlisted">{t('visibility.Unlisted')} - {t('visibility.UnlistedHint')}</option>
          <option value="Public">{t('visibility.Public')} - {t('visibility.PublicHint')}</option>
        </select>
      </div>
      <div className="form-actions" style={{ marginTop: 0 }}>
        <button type="button" className="btn" onClick={onDone}>
          {t('common.cancel')}
        </button>
        <button className="btn btn--primary" disabled={save.isPending || !title.trim()}>
          {t('common.save')}
        </button>
      </div>
    </form>
  );
}
