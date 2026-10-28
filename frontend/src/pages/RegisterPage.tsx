import { FormEvent, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { parseError } from '../api/errors';
import { useAuth } from '../auth/AuthContext';
import TextField from '../components/TextField';
import { useToast } from '../components/Toast';

export default function RegisterPage() {
  const { t } = useTranslation();
  const { user, register } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const [form, setForm] = useState({ displayName: '', userName: '', email: '', password: '', confirm: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to={from} replace />;

  const set = (key: keyof typeof form) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setFormError(null);

    const local: Record<string, string> = {};
    if (!/^[a-zA-Z0-9_.]{3,30}$/.test(form.userName)) local.userName = t('auth.userNameRule');
    if (form.password.length < 8 || !/[a-z]/.test(form.password) || !/[A-Z]/.test(form.password) || !/\d/.test(form.password))
      local.password = t('auth.passwordRule');
    if (form.password !== form.confirm) local.confirm = t('auth.passwordMismatch');
    setErrors(local);
    if (Object.keys(local).length > 0) return;

    setBusy(true);
    try {
      await register({ displayName: form.displayName.trim(), userName: form.userName.trim(), email: form.email.trim(), password: form.password });
      toast.success(t('auth.registered'));
      navigate(from, { replace: true });
    } catch (err) {
      const parsed = parseError(err);
      const server: Record<string, string> = {};
      for (const [field, messages] of Object.entries(parsed.fields)) server[field] = messages[0];
      setErrors(server);
      if (Object.keys(server).length === 0) setFormError(parsed.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <h1>{t('auth.registerTitle')}</h1>
      <p className="auth-card__sub">{t('auth.registerSub')}</p>

      <form onSubmit={submit} noValidate>
        {formError && (
          <div className="alert alert--error" role="alert">
            {formError}
          </div>
        )}
        <TextField id="displayName" label={t('auth.displayName')} value={form.displayName} onChange={set('displayName')} error={errors.displayName} autoComplete="name" required maxLength={100} autoFocus />
        <TextField id="userName" label={t('auth.userName')} value={form.userName} onChange={set('userName')} error={errors.userName} hint={t('auth.userNameRule')} autoComplete="username" required maxLength={30} />
        <TextField id="email" label={t('auth.email')} type="email" value={form.email} onChange={set('email')} error={errors.email} autoComplete="email" required />
        <TextField id="password" label={t('auth.password')} type="password" value={form.password} onChange={set('password')} error={errors.password} hint={t('auth.passwordRule')} autoComplete="new-password" required />
        <TextField id="confirm" label={t('auth.confirmPassword')} type="password" value={form.confirm} onChange={set('confirm')} error={errors.confirm} autoComplete="new-password" required />
        <button className="btn btn--primary btn--block" disabled={busy}>
          {busy ? t('common.loading') : t('auth.createAccount')}
        </button>
      </form>

      <p className="auth-card__foot">
        {t('auth.haveAccount')} <Link to="/login" state={location.state}>{t('auth.signIn')}</Link>
      </p>
    </>
  );
}
