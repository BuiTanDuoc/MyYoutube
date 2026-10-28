import { ReactNode } from 'react';

interface Props {
  icon?: ReactNode;
  title: string;
  hint?: string;
  action?: ReactNode;
}

export default function EmptyState({ icon, title, hint, action }: Props) {
  return (
    <div className="empty">
      {icon}
      <h2>{title}</h2>
      {hint && <p>{hint}</p>}
      {action}
    </div>
  );
}
