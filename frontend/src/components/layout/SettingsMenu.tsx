import { useRef, useState } from 'react';
import { Check, Languages, Monitor, Moon, Settings, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { LANGUAGES } from '../../i18n';
import { useDismiss } from '../../hooks/useDismiss';
import { ThemeMode, useTheme } from '../../theme/ThemeContext';

const THEMES: { mode: ThemeMode; icon: typeof Sun; key: string }[] = [
  { mode: 'light', icon: Sun, key: 'settings.light' },
  { mode: 'dark', icon: Moon, key: 'settings.dark' },
  { mode: 'system', icon: Monitor, key: 'settings.system' }
];

/** Theme (light / dark / system) and language (Vietnamese / English). Also used on the auth pages. */
export default function SettingsMenu() {
  const { t, i18n } = useTranslation();
  const { mode, setMode } = useTheme();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useDismiss(ref, open, () => setOpen(false));

  const current = i18n.language?.startsWith('en') ? 'en' : 'vi';

  return (
    <div className="popover" ref={ref}>
      <button
        type="button"
        className="icon-btn"
        aria-label={t('nav.settings')}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <Settings size={22} />
      </button>

      {open && (
        <div className="popover__panel" role="menu">
          <div className="menu-label">{t('settings.theme')}</div>
          {THEMES.map(({ mode: m, icon: Icon, key }) => (
            <button key={m} role="menuitemradio" aria-checked={mode === m} className="menu-item" onClick={() => setMode(m)}>
              <Icon size={18} />
              {t(key)}
              {mode === m && <Check size={16} className="menu-item__check" />}
            </button>
          ))}
          <div className="menu-sep" />
          <div className="menu-label">
            <Languages size={12} style={{ verticalAlign: '-1px' }} /> {t('settings.language')}
          </div>
          {LANGUAGES.map((l) => (
            <button
              key={l.code}
              role="menuitemradio"
              aria-checked={current === l.code}
              className="menu-item"
              onClick={() => void i18n.changeLanguage(l.code)}
            >
              {l.label}
              {current === l.code && <Check size={16} className="menu-item__check" />}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
