import { FormEvent, useState } from 'react';
import { Link } from 'react-router-dom';
import { MailCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { authApi } from '../api/endpoints';
import { parseError } from '../api/errors';
import TextField from '../components/TextField';

export default function ForgotPasswordPage() {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await authApi.forgotPassword(email.trim());
      setSent(true); // always the same answer: the API never reveals whether the email exists
    } catch (err) {
      setError(parseError(err).message);
    } finally {
      setBusy(false);
    }
  };

  if (sent) {
    return (
      <>
        <MailCheck size={44} color="var(--success)" style={{ marginTop: 12 }} />
        <h1>{t('auth.forgotSentTitle')}</h1>
        <p className="auth-card__sub">{t('auth.forgotSentText')}</p>
        <Link to="/login" className="btn btn--block">
          {t('auth.backToSignIn')}
        </Link>
      </>
    );
  }

  return (
    <>
      <h1>{t('auth.forgotTitle')}</h1>
      <p className="auth-card__sub">{t('auth.forgotSub')}</p>
      <form onSubmit={submit} noValidate>
        {error && (
          <div className="alert alert--error" role="alert">
            {error}
          </div>
        )}
        <TextField id="email" label={t('auth.email')} type="email" value={email} onChange={setEmail} autoComplete="email" required autoFocus />
        <button className="btn btn--primary btn--block" disabled={busy || !email}>
          {busy ? t('common.loading') : t('auth.sendLink')}
        </button>
      </form>
      <p className="auth-card__foot">
        <Link to="/login">{t('auth.backToSignIn')}</Link>
      </p>
    </>
  );
}
