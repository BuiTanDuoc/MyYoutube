import { FormEvent, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { authApi } from '../api/endpoints';
import { parseError } from '../api/errors';
import TextField from '../components/TextField';
import { useToast } from '../components/Toast';

export default function ResetPasswordPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const toast = useToast();
  const [params] = useSearchParams();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [fieldError, setFieldError] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);

  if (!email || !token) {
    return (
      <>
        <h1>{t('auth.confirmFailedTitle')}</h1>
        <p className="auth-card__sub">{t('auth.resetInvalid')}</p>
        <Link to="/forgot-password" className="btn btn--primary btn--block">
          {t('auth.sendLink')}
        </Link>
      </>
    );
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setFieldError(undefined);

    if (password.length < 8 || !/[a-z]/.test(password) || !/[A-Z]/.test(password) || !/\d/.test(password)) {
      setFieldError(t('auth.passwordRule'));
      return;
    }
    if (password !== confirm) {
      setFieldError(t('auth.passwordMismatch'));
      return;
    }

    setBusy(true);
    try {
      await authApi.resetPassword({ email, token, newPassword: password });
      toast.success(t('auth.resetDone'));
      navigate('/login', { replace: true });
    } catch (err) {
      const parsed = parseError(err);
      if (parsed.code === 'invalid_token') setError(t('auth.resetInvalid'));
      else if (parsed.fields.password) setFieldError(parsed.fields.password[0]);
      else setError(parsed.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <h1>{t('auth.resetTitle')}</h1>
      <p className="auth-card__sub">{email}</p>
      <form onSubmit={submit} noValidate>
        {error && (
          <div className="alert alert--error" role="alert">
            {error}
          </div>
        )}
        <TextField id="password" label={t('auth.newPassword')} type="password" value={password} onChange={setPassword} error={fieldError} hint={t('auth.passwordRule')} autoComplete="new-password" required autoFocus />
        <TextField id="confirm" label={t('auth.confirmPassword')} type="password" value={confirm} onChange={setConfirm} autoComplete="new-password" required />
        <button className="btn btn--primary btn--block" disabled={busy}>
          {busy ? t('common.loading') : t('auth.resetSubmit')}
        </button>
      </form>
    </>
  );
}
