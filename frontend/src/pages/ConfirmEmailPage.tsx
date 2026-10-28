import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { CheckCircle2, XCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { refreshSession } from '../api/client';
import { authApi } from '../api/endpoints';
import { parseError } from '../api/errors';
import { tokens } from '../api/tokens';
import { useAuth } from '../auth/AuthContext';
import Spinner from '../components/Spinner';
import { useToast } from '../components/Toast';

export default function ConfirmEmailPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const toast = useToast();
  const [params] = useSearchParams();
  const [state, setState] = useState<'loading' | 'ok' | 'error'>('loading');
  const [resending, setResending] = useState(false);
  const started = useRef(false); // React StrictMode runs effects twice in development

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    const userId = params.get('userId');
    const token = params.get('token');
    if (!userId || !token) {
      setState('error');
      return;
    }

    authApi
      .confirmEmail(userId, token)
      .then(async () => {
        // The "email verified" flag lives inside the access token: get a fresh one
        if (tokens.refresh) await refreshSession();
        setState('ok');
      })
      .catch(() => setState('error'));
  }, [params]);

  const resend = async () => {
    if (!user) return;
    setResending(true);
    try {
      await authApi.resendConfirmation(user.email);
      toast.success(t('auth.confirmResent'));
    } catch (e) {
      toast.error(parseError(e).message);
    } finally {
      setResending(false);
    }
  };

  if (state === 'loading') return <Spinner />;

  if (state === 'ok') {
    return (
      <>
        <CheckCircle2 size={44} color="var(--success)" style={{ marginTop: 12 }} />
        <h1>{t('auth.confirmedTitle')}</h1>
        <p className="auth-card__sub">{t('auth.confirmedText')}</p>
        <Link to="/" className="btn btn--primary btn--block">
          {t('notFound.home')}
        </Link>
      </>
    );
  }

  return (
    <>
      <XCircle size={44} color="var(--danger)" style={{ marginTop: 12 }} />
      <h1>{t('auth.confirmFailedTitle')}</h1>
      <p className="auth-card__sub">{t('auth.confirmFailedText')}</p>
      {user && !user.emailConfirmed ? (
        <button className="btn btn--primary btn--block" onClick={() => void resend()} disabled={resending}>
          {t('auth.resendConfirmation')}
        </button>
      ) : (
        <Link to="/login" className="btn btn--primary btn--block">
          {t('auth.signIn')}
        </Link>
      )}
    </>
  );
}
