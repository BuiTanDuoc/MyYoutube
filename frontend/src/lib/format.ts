import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

export function formatDuration(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  const two = (n: number) => String(n).padStart(2, '0');
  return h > 0 ? `${h}:${two(m)}:${two(sec)}` : `${m}:${two(sec)}`;
}

export function formatBytes(bytes: number): string {
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(1)} GB`;
  if (bytes >= 1024 ** 2) return `${(bytes / 1024 ** 2).toFixed(1)} MB`;
  return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600],
  ['month', 30 * 24 * 3600],
  ['week', 7 * 24 * 3600],
  ['day', 24 * 3600],
  ['hour', 3600],
  ['minute', 60]
];

/** Locale-aware helpers (compact numbers, "3 days ago", dates). Re-created when the language changes. */
export function useFormatters() {
  const { i18n } = useTranslation();
  const lang = i18n.language?.startsWith('en') ? 'en' : 'vi';

  return useMemo(() => {
    const compact = new Intl.NumberFormat(lang, { notation: 'compact', maximumFractionDigits: 1 });
    const relative = new Intl.RelativeTimeFormat(lang, { numeric: 'auto' });
    const date = new Intl.DateTimeFormat(lang, { year: 'numeric', month: 'long', day: 'numeric' });

    return {
      compact: (n: number) => compact.format(n),
      full: (n: number) => new Intl.NumberFormat(lang).format(n),
      date: (iso: string) => date.format(new Date(iso)),
      timeAgo: (iso: string) => {
        const seconds = Math.round((Date.now() - new Date(iso).getTime()) / 1000);
        for (const [unit, size] of UNITS) {
          if (seconds >= size) return relative.format(-Math.floor(seconds / size), unit);
        }
        return relative.format(0, 'second'); // "now"
      }
    };
  }, [lang]);
}
