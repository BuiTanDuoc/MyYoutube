import { HTMLInputTypeAttribute, ReactNode } from 'react';

interface Props {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: HTMLInputTypeAttribute;
  autoComplete?: string;
  error?: string;
  hint?: ReactNode;
  required?: boolean;
  maxLength?: number;
  autoFocus?: boolean;
}

export default function TextField({ id, label, value, onChange, type = 'text', autoComplete, error, hint, required, maxLength, autoFocus }: Props) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        className="input"
        type={type}
        value={value}
        autoComplete={autoComplete}
        required={required}
        maxLength={maxLength}
        autoFocus={autoFocus}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
        onChange={(e) => onChange(e.target.value)}
      />
      {error ? (
        <span id={`${id}-error`} className="field__error">
          {error}
        </span>
      ) : (
        hint && (
          <span id={`${id}-hint`} className="field__hint">
            {hint}
          </span>
        )
      )}
    </div>
  );
}
