import { Link } from 'react-router-dom';

export function LogoMark() {
  return (
    <svg viewBox="0 0 32 22" aria-hidden="true">
      <rect width="32" height="22" rx="7" fill="#ff0033" />
      <path d="M13 6l8 5-8 5z" fill="#fff" />
    </svg>
  );
}

export default function Logo() {
  return (
    <Link to="/" className="logo" aria-label="MiniTube">
      <LogoMark />
      <span>MiniTube</span>
    </Link>
  );
}
