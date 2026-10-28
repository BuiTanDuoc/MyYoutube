import { Link } from 'react-router-dom';
import { SearchX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import EmptyState from '../components/EmptyState';

export default function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <EmptyState
      icon={<SearchX size={56} />}
      title={t('notFound.title')}
      hint={t('notFound.hint')}
      action={
        <Link to="/" className="btn btn--primary">
          {t('notFound.home')}
        </Link>
      }
    />
  );
}
