import { Outlet } from 'react-router-dom';
import Logo from '../components/Logo';
import SettingsMenu from '../components/layout/SettingsMenu';

/** Centered card used by sign in / register / password pages. */
export default function AuthLayout() {
  return (
    <div className="auth-page">
      <div className="auth-tools">
        <SettingsMenu />
      </div>
      <div className="auth-card">
        <Logo />
        <Outlet />
      </div>
    </div>
  );
}
