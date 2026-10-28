import { FormEvent, KeyboardEvent, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Search, UserRound } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { searchApi } from '../../api/endpoints';
import type { Suggestion } from '../../api/types';
import { useDebounce } from '../../hooks/useDebounce';
import { useDismiss } from '../../hooks/useDismiss';

export default function SearchBox() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const [text, setText] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const wrapRef = useRef<HTMLDivElement>(null);
  useDismiss(wrapRef, open, () => setOpen(false));

  // Keep the box in sync with the URL on the results page, clear it elsewhere
  useEffect(() => {
    setText(location.pathname === '/results' ? new URLSearchParams(location.search).get('q') ?? '' : '');
  }, [location.pathname, location.search]);

  const debounced = useDebounce(text.trim(), 250);
  const { data: suggestions = [] } = useQuery({
    queryKey: ['suggest', debounced],
    queryFn: ({ signal }) => searchApi.suggestions(debounced, signal),
    enabled: open && debounced.length > 0,
    staleTime: 60_000
  });

  const shown = open && debounced.length > 0 ? suggestions : [];

  const run = (query: string) => {
    const q = query.trim();
    if (!q) return;
    setOpen(false);
    navigate(`/results?q=${encodeURIComponent(q)}`);
  };

  const pick = (s: Suggestion) => {
    setOpen(false);
    if (s.type === 'channel' && s.id) navigate(`/channel/${s.id}`);
    else run(s.text);
  };

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    const chosen = shown[active];
    if (chosen) pick(chosen);
    else run(text);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown' && shown.length) {
      e.preventDefault();
      setActive((i) => (i + 1) % shown.length);
    } else if (e.key === 'ArrowUp' && shown.length) {
      e.preventDefault();
      setActive((i) => (i <= 0 ? shown.length - 1 : i - 1));
    }
  };

  return (
    <div className="searchbox" ref={wrapRef} role="search">
      <form className="searchbox__form" onSubmit={onSubmit}>
        <input
          className="searchbox__input"
          type="search"
          value={text}
          placeholder={t('search.placeholder')}
          aria-label={t('search.placeholder')}
          aria-autocomplete="list"
          aria-expanded={shown.length > 0}
          autoComplete="off"
          maxLength={100}
          onChange={(e) => {
            setText(e.target.value);
            setActive(-1);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={onKeyDown}
        />
        <button type="submit" className="searchbox__submit" aria-label={t('search.button')}>
          <Search size={20} />
        </button>
      </form>

      {shown.length > 0 && (
        <ul className="suggestions" role="listbox">
          {shown.map((s, i) => (
            <li key={`${s.type}-${s.text}-${i}`} className={i === active ? 'is-active' : ''} role="option" aria-selected={i === active}>
              <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={() => pick(s)}>
                {s.type === 'channel' ? <UserRound size={18} /> : <Search size={18} />}
                <span>{s.text}</span>
                {s.type === 'channel' && <span className="muted">{t('search.suggestionChannel')}</span>}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
