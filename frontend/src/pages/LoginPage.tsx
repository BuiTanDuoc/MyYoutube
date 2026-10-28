import { FormEvent, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { parseError } from '../api/errors';
import { useAuth } from '../auth/AuthContext';
import TextField from '../components/TextField';

export default function LoginPage() {
  const { t } = useTranslation();
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to={from} replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(identifier.trim(), password);
      navigate(from, { replace: true });
    } catch (err) {
      const parsed = parseError(err);
      setError(parsed.code === 'account_locked' ? t('auth.locked') : parsed.status === 401 ? t('auth.invalidCredentials') : parsed.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <h1>{t('auth.signInTitle')}</h1>
      <p className="auth-card__sub">{t('auth.signInSub')}</p>

      <form onSubmit={submit} noValidate>
        {error && (
          <div className="alert alert--error" role="alert">
            {error}
          </div>
        )}
        <TextField id="identifier" label={t('auth.emailOrUser')} value={identifier} onChange={setIdentifier} autoComplete="username" required autoFocus />
        <TextField id="password" label={t('auth.password')} type="password" value={password} onChange={setPassword} autoComplete="current-password" required />
        <p style={{ margin: '-6px 0 16px' }}>
          <Link to="/forgot-password" className="link">
            {t('auth.forgotLink')}
          </Link>
        </p>
        <button className="btn btn--primary btn--block" disabled={busy || !identifier || !password}>
          {busy ? t('common.loading') : t('auth.signIn')}
        </button>
      </form>

      <p className="auth-card__foot">
        {t('auth.noAccount')} <Link to="/register" state={location.state}>{t('auth.createAccount')}</Link>
      </p>
    </>
  );
}
