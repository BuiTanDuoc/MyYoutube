import { DragEvent, FormEvent, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, FileVideo, TriangleAlert, UploadCloud } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { refreshSession } from '../api/client';
import { authApi, videosApi } from '../api/endpoints';
import { parseError } from '../api/errors';
import type { Visibility } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { useToast } from '../components/Toast';
import { formatBytes } from '../lib/format';

const ALLOWED = ['.mp4', '.mov', '.m4v', '.mkv', '.webm', '.avi'];
const MAX_BYTES = 2 * 1024 * 1024 * 1024; // keep in sync with Video:MaxUploadBytes on the API

type Phase = 'form' | 'uploading' | 'processing' | 'done' | 'error';

export default function UploadPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const toast = useToast();

  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [visibility, setVisibility] = useState<Visibility>('Private');
  const [phase, setPhase] = useState<Phase>('form');
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [videoId, setVideoId] = useState<string | null>(null); // kept so a retry re-uploads to the same record
  const [dragging, setDragging] = useState(false);
  const [checking, setChecking] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    document.title = `${t('upload.title')} - MiniTube`;
  }, [t]);

  // Do not lose a running upload by accident
  useEffect(() => {
    if (phase !== 'uploading') return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [phase]);

  // After the file is sent the API converts it in the background: poll until it is Ready or Failed
  const status = useQuery({
    queryKey: ['upload-status', videoId],
    queryFn: () => videosApi.get(videoId!),
    enabled: phase === 'processing' && !!videoId,
    refetchInterval: 2500
  });

  useEffect(() => {
    const v = status.data;
    if (!v || phase !== 'processing') return;
    if (v.status === 'Ready') setPhase('done');
    else if (v.status === 'Failed') {
      setError(v.processingError ?? t('upload.processingFailed'));
      setPhase('error');
    }
  }, [status.data, phase, t]);

  const chooseFile = (f: File | null) => {
    setFileError(null);
    if (!f) return;

    const ext = f.name.slice(f.name.lastIndexOf('.')).toLowerCase();
    if (!ALLOWED.includes(ext)) {
      setFileError(t('upload.badType', { types: ALLOWED.join(', ') }));
      return;
    }
    if (f.size > MAX_BYTES) {
      setFileError(t('upload.tooLarge', { size: formatBytes(MAX_BYTES) }));
      return;
    }
    setFile(f);
    if (!title.trim()) setTitle(f.name.replace(/\.[^.]+$/, '').slice(0, 200));
  };

  const onDrop = (e: DragEvent) => {
    e.preventDefault();
    setDragging(false);
    chooseFile(e.dataTransfer.files[0] ?? null);
  };

  const start = async (e?: FormEvent) => {
    e?.preventDefault();
    if (!file || !title.trim()) return;

    setPhase('uploading');
    setError(null);
    setProgress(0);
    abortRef.current = new AbortController();

    try {
      let id = videoId;
      if (!id) {
        const created = await videosApi.create({ title: title.trim(), description: description.trim() || null, visibility });
        id = created.id;
        setVideoId(id);
      }
      await videosApi.upload(id, file, setProgress, abortRef.current.signal);
      setPhase('processing');
    } catch (err) {
      if (axios.isCancel(err)) {
        setPhase('form');
        return;
      }
      setError(parseError(err).message);
      setPhase('error');
    }
  };

  const reset = () => {
    setFile(null);
    setTitle('');
    setDescription('');
    setVisibility('Private');
    setVideoId(null);
    setProgress(0);
    setError(null);
    setPhase('form');
  };

  // The API only lets verified accounts upload
  if (user && !user.emailConfirmed) {
    const resend = async () => {
      try {
        await authApi.resendConfirmation(user.email);
        toast.success(t('auth.confirmResent'));
      } catch (e) {
        toast.error(parseError(e).message);
      }
    };
    const recheck = async () => {
      setChecking(true);
      const session = await refreshSession(); // picks up "email verified" if it was confirmed in the meantime
      setChecking(false);
      if (!session?.user.emailConfirmed) toast.show(t('upload.stillUnverified'));
    };

    return (
      <div className="page-narrow">
        <h1 className="page-title">{t('upload.title')}</h1>
        <div className="alert alert--warn" role="alert">
          <TriangleAlert size={20} style={{ flex: 'none' }} />
          <div>
            <strong>{t('upload.verifyTitle')}</strong>
            <p>{t('upload.verifyText', { email: user.email })}</p>
          </div>
        </div>
        <div className="form-actions" style={{ justifyContent: 'flex-start' }}>
          <button className="btn btn--primary" onClick={() => void resend()}>
            {t('auth.resendConfirmation')}
          </button>
          <button className="btn" onClick={() => void recheck()} disabled={checking}>
            {t('upload.iConfirmed')}
          </button>
        </div>
      </div>
    );
  }

  const busy = phase === 'uploading' || phase === 'processing';

  return (
    <div className="page-narrow">
      <h1 className="page-title">{t('upload.title')}</h1>

      {phase === 'done' && videoId ? (
        <div className="upload-status" role="status">
          <div className="upload-status__row">
            <span style={{ display: 'inline-flex', gap: 8, alignItems: 'center' }}>
              <CheckCircle2 color="var(--success)" /> <strong>{t('upload.ready')}</strong>
            </span>
          </div>
          <div className="form-actions" style={{ justifyContent: 'flex-start' }}>
            <Link to={`/watch/${videoId}`} className="btn btn--primary">
              {t('upload.watch')}
            </Link>
            <Link to="/studio" className="btn">
              {t('nav.studio')}
            </Link>
            <button className="btn" onClick={reset}>
              {t('upload.another')}
            </button>
          </div>
        </div>
      ) : (
        <form onSubmit={start} noValidate>
          {phase === 'error' && error && (
            <div className="alert alert--error" role="alert">
              <TriangleAlert size={20} style={{ flex: 'none' }} />
              <div>
                <strong>{t('upload.failed')}</strong>
                <p>{error}</p>
              </div>
            </div>
          )}

          {!file ? (
            <>
              <div
                className={`dropzone ${dragging ? 'is-over' : ''}`}
                onClick={() => inputRef.current?.click()}
                onDragOver={(e) => {
                  e.preventDefault();
                  setDragging(true);
                }}
                onDragLeave={() => setDragging(false)}
                onDrop={onDrop}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') inputRef.current?.click();
                }}
              >
                <UploadCloud size={44} />
                <strong>{t('upload.drop')}</strong>
                <span>{t('upload.dropHint', { types: ALLOWED.join(' '), size: formatBytes(MAX_BYTES) })}</span>
              </div>
              {fileError && <p className="field__error" style={{ marginTop: -12, marginBottom: 16 }}>{fileError}</p>}
            </>
          ) : (
            <div className="upload-status" style={{ marginBottom: 20 }}>
              <div className="upload-status__row">
                <span style={{ display: 'inline-flex', gap: 10, alignItems: 'center', minWidth: 0 }}>
                  <FileVideo size={28} style={{ flex: 'none' }} />
                  <span style={{ overflowWrap: 'anywhere' }}>
                    <strong>{file.name}</strong>
                    <br />
                    <span className="muted">{formatBytes(file.size)}</span>
                  </span>
                </span>
                {!busy && (
                  <button type="button" className="btn btn--sm" onClick={() => setFile(null)}>
                    {t('upload.changeFile')}
                  </button>
                )}
              </div>

              {phase === 'uploading' && (
                <>
                  <div className="progress" role="progressbar" aria-valuenow={Math.round(progress * 100)} aria-valuemin={0} aria-valuemax={100}>
                    <span style={{ width: `${progress * 100}%` }} />
                  </div>
                  <div className="upload-status__row">
                    <span className="muted">{t('upload.uploading', { percent: Math.round(progress * 100) })}</span>
                    <button type="button" className="btn btn--sm" onClick={() => abortRef.current?.abort()}>
                      {t('common.cancel')}
                    </button>
                  </div>
                </>
              )}

              {phase === 'processing' && (
                <>
                  <div className="progress" role="progressbar" aria-valuenow={status.data?.processingProgress ?? 0} aria-valuemin={0} aria-valuemax={100}>
                    <span style={{ width: `${status.data?.processingProgress ?? 0}%` }} />
                  </div>
                  <span className="muted">
                    {status.data?.status === 'Queued' ? t('upload.queued') : t('upload.processing', { percent: status.data?.processingProgress ?? 0 })}
                  </span>
                  <p className="field__hint">{t('upload.canLeave')}</p>
                </>
              )}
            </div>
          )}

          <div className="field">
            <label htmlFor="title">{t('upload.videoTitle')}</label>
            <input id="title" className="input" value={title} maxLength={200} disabled={busy || !!videoId} onChange={(e) => setTitle(e.target.value)} required />
          </div>
          <div className="field">
            <label htmlFor="description">{t('upload.description')}</label>
            <textarea id="description" className="textarea" value={description} maxLength={5000} disabled={busy || !!videoId} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="visibility">{t('upload.visibility')}</label>
            <select id="visibility" className="select" value={visibility} disabled={busy || !!videoId} onChange={(e) => setVisibility(e.target.value as Visibility)}>
              <option value="Private">{t('visibility.Private')} - {t('visibility.PrivateHint')}</option>
              <option value="Unlisted">{t('visibility.Unlisted')} - {t('visibility.UnlistedHint')}</option>
              <option value="Public">{t('visibility.Public')} - {t('visibility.PublicHint')}</option>
            </select>
            {videoId && <span className="field__hint">{t('upload.changeLater')}</span>}
          </div>

          {!busy && (
            <div className="form-actions">
              <button className="btn btn--primary" disabled={!file || !title.trim()}>
                {phase === 'error' ? t('common.retry') : t('upload.start')}
              </button>
            </div>
          )}
        </form>
      )}
    </div>
  );
}
