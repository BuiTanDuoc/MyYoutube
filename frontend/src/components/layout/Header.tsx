import { Link } from 'react-router-dom';
import { Menu, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AuthContext';
import Logo from '../Logo';
import SearchBox from './SearchBox';
import SettingsMenu from './SettingsMenu';
import UserMenu from './UserMenu';

export default function Header({ onMenu }: { onMenu: () => void }) {
  const { t } = useTranslation();
  const { user } = useAuth();

  return (
    <header className="header">
      <div className="header__start">
        <button type="button" className="icon-btn" onClick={onMenu} aria-label={t('nav.menu')}>
          <Menu size={22} />
        </button>
        <Logo />
      </div>

      <SearchBox />

      <div className="header__end">
        {user && (
          <Link to="/upload" className="icon-btn header__hide-sm" aria-label={t('nav.upload')} title={t('nav.upload')}>
            <Upload size={22} />
          </Link>
        )}
        <SettingsMenu />
        {user ? (
          <UserMenu />
        ) : (
          <Link to="/login" className="btn btn--outline">
            {t('nav.signIn')}
          </Link>
        )}
      </div>
    </header>
  );
}
