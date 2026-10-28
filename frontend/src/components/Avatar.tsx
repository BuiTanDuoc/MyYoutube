import { apiUrl } from '../lib/url';

interface Props {
  name: string;
  src?: string | null;
  size?: number;
}

export default function Avatar({ name, src, size = 36 }: Props) {
  const initial = name.trim().charAt(0).toUpperCase() || '?';
  return (
    <span className="avatar" style={{ width: size, height: size, fontSize: size * 0.42 }} aria-hidden="true">
      {src ? <img src={apiUrl(src)} alt="" loading="lazy" /> : initial}
    </span>
  );
}
