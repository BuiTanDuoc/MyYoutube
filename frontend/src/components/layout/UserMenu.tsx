import { useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Clapperboard, LogOut } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AuthContext';
import { useDismiss } from '../../hooks/useDismiss';
import Avatar from '../Avatar';

export default function UserMenu() {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useDismiss(ref, open, () => setOpen(false));

  if (!user) return null;

  const signOut = async () => {
    setOpen(false);
    await logout();
    navigate('/');
  };

  return (
    <div className="popover" ref={ref}>
      <button
        type="button"
        className="icon-btn"
        aria-label={user.displayName}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <Avatar name={user.displayName} src={user.avatarUrl} size={32} />
      </button>

      {open && (
        <div className="popover__panel" role="menu">
          <div className="menu-item" style={{ cursor: 'default' }}>
            <Avatar name={user.displayName} src={user.avatarUrl} size={40} />
            <div>
              <strong>{user.displayName}</strong>
              <div className="muted">@{user.userName}</div>
            </div>
          </div>
          <div className="menu-sep" />
          <Link to={`/channel/${user.id}`} className="menu-item" role="menuitem" onClick={() => setOpen(false)}>
            <Avatar name={user.displayName} src={user.avatarUrl} size={18} />
            {t('nav.yourChannel')}
          </Link>
          <Link to="/studio" className="menu-item" role="menuitem" onClick={() => setOpen(false)}>
            <Clapperboard size={18} />
            {t('nav.studio')}
          </Link>
          <div className="menu-sep" />
          <button className="menu-item" role="menuitem" onClick={() => void signOut()}>
            <LogOut size={18} />
            {t('nav.signOut')}
          </button>
        </div>
      )}
    </div>
  );
}
