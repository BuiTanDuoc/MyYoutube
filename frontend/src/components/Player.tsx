import { KeyboardEvent, useEffect, useRef, useState } from 'react';
import Hls from 'hls.js';
import { Settings } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { videosApi, sendProgress } from '../api/endpoints';
import { tokens } from '../api/tokens';
import type { VideoDto } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { apiUrl } from '../lib/url';

interface Props {
  video: VideoDto;
  /** Seconds to resume from (0 = beginning). Only read when the player mounts. */
  startAt: number;
  onEnded?: () => void;
  onViewCounted?: (viewCount: number) => void;
}

interface Level {
  index: number;
  height: number;
}

const HEARTBEAT_MS = 15_000;
const MAX_NETWORK_RETRIES = 3;

/**
 * HLS player (hls.js, native HLS on Safari) with a quality menu, resume, watch-progress heartbeat
 * and the "count a view after min(30 s, 50 %) of real watching" rule the API expects.
 */
export default function Player({ video, startAt, onEnded, onViewCounted }: Props) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const wrapRef = useRef<HTMLDivElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const hlsRef = useRef<Hls | null>(null);

  const [levels, setLevels] = useState<Level[]>([]);
  const [selected, setSelected] = useState(-1); // -1 = automatic
  const [activeHeight, setActiveHeight] = useState<number | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [failed, setFailed] = useState(false);

  // Latest values for the long-lived effect below (it must not restart when these change)
  const signedIn = useRef(!!user);
  signedIn.current = !!user;
  const endedRef = useRef(onEnded);
  endedRef.current = onEnded;
  const viewRef = useRef(onViewCounted);
  viewRef.current = onViewCounted;
  const startRef = useRef(startAt);

  useEffect(() => {
    const el = videoRef.current;
    if (!el || !video.streamUrl) return;

    const src = apiUrl(video.streamUrl);
    const resumeAt = startRef.current;

    let watched = 0;
    let lastTime = 0;
    let counted = false;
    let hasPlayed = false;
    let networkRetries = 0;

    setFailed(false);
    setLevels([]);
    setSelected(-1);
    setActiveHeight(null);

    let hls: Hls | null = null;

    if (Hls.isSupported()) {
      hls = new Hls({
        startPosition: resumeAt > 0 ? resumeAt : -1,
        // Private videos need the Bearer token on every playlist/segment request
        xhrSetup: (xhr) => {
          if (tokens.access) xhr.setRequestHeader('Authorization', `Bearer ${tokens.access}`);
        }
      });
      hlsRef.current = hls;
      hls.loadSource(src);
      hls.attachMedia(el);

      hls.on(Hls.Events.MANIFEST_PARSED, (_e, data) => {
        setLevels(data.levels.map((l, index) => ({ index, height: l.height })).sort((a, b) => b.height - a.height));
        el.play().catch(() => undefined); // may be blocked until the user interacts
      });
      hls.on(Hls.Events.LEVEL_SWITCHED, (_e, data) => setActiveHeight(hls?.levels[data.level]?.height ?? null));
      hls.on(Hls.Events.ERROR, (_e, data) => {
        if (!data.fatal) return;
        if (data.type === Hls.ErrorTypes.NETWORK_ERROR && networkRetries < MAX_NETWORK_RETRIES) {
          networkRetries += 1;
          hls?.startLoad();
        } else if (data.type === Hls.ErrorTypes.MEDIA_ERROR) {
          hls?.recoverMediaError();
        } else {
          setFailed(true);
          hls?.destroy();
        }
      });
    } else if (el.canPlayType('application/vnd.apple.mpegurl')) {
      // iOS Safari: native HLS (cannot send the Authorization header, so private videos will not play there)
      el.src = src;
      el.addEventListener(
        'loadedmetadata',
        () => {
          if (resumeAt > 0) el.currentTime = resumeAt;
          el.play().catch(() => undefined);
        },
        { once: true }
      );
    } else {
      setFailed(true);
    }

    const saveProgress = () => {
      if (hasPlayed && signedIn.current && el.currentTime > 0) sendProgress(video.id, el.currentTime);
    };

    const onPlay = () => {
      hasPlayed = true;
    };

    const onTimeUpdate = () => {
      const now = el.currentTime;
      const delta = now - lastTime;
      lastTime = now;

      // Only normal playback counts: seeking jumps far more than 2 s between two timeupdate events
      if (!el.paused && delta > 0 && delta < 2) watched += delta;

      const duration = Number.isFinite(el.duration) && el.duration > 0 ? el.duration : video.durationSeconds;
      if (!counted && duration > 0 && watched >= Math.min(30, duration * 0.5)) {
        counted = true;
        videosApi
          .view(video.id)
          .then((r) => viewRef.current?.(r.viewCount))
          .catch(() => undefined);
      }
    };

    const onEndedEvent = () => {
      saveProgress();
      endedRef.current?.();
    };

    const onVisibility = () => {
      if (document.visibilityState === 'hidden') saveProgress();
    };

    const heartbeat = window.setInterval(() => {
      if (!el.paused) saveProgress();
    }, HEARTBEAT_MS);

    el.addEventListener('play', onPlay);
    el.addEventListener('timeupdate', onTimeUpdate);
    el.addEventListener('pause', saveProgress);
    el.addEventListener('seeked', saveProgress);
    el.addEventListener('ended', onEndedEvent);
    document.addEventListener('visibilitychange', onVisibility);
    window.addEventListener('pagehide', saveProgress);

    return () => {
      saveProgress(); // leaving the page / switching video
      window.clearInterval(heartbeat);
      el.removeEventListener('play', onPlay);
      el.removeEventListener('timeupdate', onTimeUpdate);
      el.removeEventListener('pause', saveProgress);
      el.removeEventListener('seeked', saveProgress);
      el.removeEventListener('ended', onEndedEvent);
      document.removeEventListener('visibilitychange', onVisibility);
      window.removeEventListener('pagehide', saveProgress);
      hls?.destroy();
      hlsRef.current = null;
    };
  }, [video.id, video.streamUrl, video.durationSeconds]);

  const chooseLevel = (index: number) => {
    if (hlsRef.current) hlsRef.current.nextLevel = index; // -1 = automatic
    setSelected(index);
    setMenuOpen(false);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    const el = videoRef.current;
    if (!el || (e.target as HTMLElement).tagName === 'INPUT') return;

    switch (e.key) {
      case 'k':
        void (el.paused ? el.play() : el.pause());
        break;
      case 'j':
        el.currentTime = Math.max(0, el.currentTime - 10);
        break;
      case 'l':
        el.currentTime = Math.min(el.duration || Infinity, el.currentTime + 10);
        break;
      case 'm':
        el.muted = !el.muted;
        break;
      case 'f':
        if (document.fullscreenElement) void document.exitFullscreen();
        else void wrapRef.current?.requestFullscreen();
        break;
      default:
        return;
    }
    e.preventDefault();
  };

  const qualityLabel =
    selected === -1
      ? `${t('player.auto')}${activeHeight ? ` (${activeHeight}p)` : ''}`
      : `${levels.find((l) => l.index === selected)?.height ?? ''}p`;

  return (
    <div className="player" ref={wrapRef} onKeyDown={onKeyDown}>
      <video
        ref={videoRef}
        controls
        playsInline
        preload="metadata"
        poster={video.thumbnailUrl ? apiUrl(video.thumbnailUrl) : undefined}
        aria-label={video.title}
      />

      {levels.length > 1 && (
        <div className={`player__quality ${menuOpen ? 'is-open' : ''}`}>
          <button type="button" className="player__quality-btn" aria-haspopup="menu" aria-expanded={menuOpen} onClick={() => setMenuOpen((o) => !o)}>
            <Settings size={15} />
            {qualityLabel}
          </button>
          {menuOpen && (
            <ul role="menu">
              <li>
                <button role="menuitemradio" aria-checked={selected === -1} onClick={() => chooseLevel(-1)}>
                  {t('player.auto')}
                  {selected === -1 && <span>✓</span>}
                </button>
              </li>
              {levels.map((l) => (
                <li key={l.index}>
                  <button role="menuitemradio" aria-checked={selected === l.index} onClick={() => chooseLevel(l.index)}>
                    {l.height}p
                    {selected === l.index && <span>✓</span>}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {failed && (
        <div className="player__error" role="alert">
          <div>
            <strong>{t('player.error')}</strong>
            <p>{t('player.errorHint')}</p>
          </div>
        </div>
      )}
    </div>
  );
}
