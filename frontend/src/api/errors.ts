import axios from 'axios';
import i18n from '../i18n';

export interface ParsedError {
  message: string;
  status?: number;
  /** Machine-readable code sent by the API (invalid_token, account_locked, ...). */
  code?: string;
  /** Validation messages by field, keys in camelCase. */
  fields: Record<string, string[]>;
}

interface Problem {
  title?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
}

const camel = (key: string) => key.charAt(0).toLowerCase() + key.slice(1);

export function parseError(err: unknown): ParsedError {
  if (axios.isAxiosError(err)) {
    if (!err.response) return { message: i18n.t('errors.network'), fields: {} };

    const status = err.response.status;
    const data = (err.response.data ?? {}) as Problem;

    const fields: Record<string, string[]> = {};
    for (const [key, messages] of Object.entries(data.errors ?? {})) fields[camel(key)] = messages;

    let message = data.title || i18n.t('errors.generic');
    if (status === 429) message = i18n.t('errors.tooMany');
    else if (status === 413) message = data.title || i18n.t('errors.tooLarge');
    else if (status >= 500) message = i18n.t('errors.server');
    else if (status === 400 && data.title === 'One or more validation errors occurred.') {
      message = Object.values(fields).flat()[0] ?? i18n.t('errors.generic');
    }

    return { message, status, code: data.code, fields };
  }

  return { message: err instanceof Error ? err.message : i18n.t('errors.generic'), fields: {} };
}
