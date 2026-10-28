import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Header from './Header';
import Sidebar from './Sidebar';

const COLLAPSE_KEY = 'mt.sidebar';

export default function Layout() {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(COLLAPSE_KEY) === 'collapsed');

  // Like YouTube, the watch page gives the video all the room: the sidebar becomes a drawer there
  const overlay = pathname.startsWith('/watch');

  useEffect(() => setDrawerOpen(false), [pathname]);

  const toggle = () => {
    const small = window.matchMedia('(max-width: 1023px)').matches;
    if (small || overlay) {
      setDrawerOpen((o) => !o);
    } else {
      setCollapsed((c) => {
        localStorage.setItem(COLLAPSE_KEY, c ? 'expanded' : 'collapsed');
        return !c;
      });
    }
  };

  return (
    <div className={`shell ${collapsed ? 'is-collapsed' : ''} ${overlay ? 'is-overlay' : ''}`}>
      <a className="skip-link" href="#main">
        {t('a11y.skipToContent')}
      </a>
      <Header onMenu={toggle} />
      <Sidebar open={drawerOpen} onNavigate={() => setDrawerOpen(false)} />
      {drawerOpen && <div className="backdrop" onClick={() => setDrawerOpen(false)} />}
      <main id="main" className="main">
        <Outlet />
      </main>
    </div>
  );
}
