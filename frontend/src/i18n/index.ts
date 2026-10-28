import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';
import en from './en.json';
import vi from './vi.json';

export const LANGUAGES = [
  { code: 'vi', label: 'Tiếng Việt' },
  { code: 'en', label: 'English' }
] as const;

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: { en: { translation: en }, vi: { translation: vi } },
    supportedLngs: ['vi', 'en'],
    nonExplicitSupportedLngs: true, // en-US -> en
    fallbackLng: 'vi',
    interpolation: { escapeValue: false },
    detection: { order: ['localStorage', 'navigator'], lookupLocalStorage: 'mt.lang', caches: ['localStorage'] }
  });

const syncHtmlLang = (lng: string) => {
  document.documentElement.lang = lng.startsWith('en') ? 'en' : 'vi';
};
syncHtmlLang(i18n.language || 'vi');
i18n.on('languageChanged', syncHtmlLang);

export default i18n;
