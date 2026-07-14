// LOC001 (#744) — UI language selector. Presentation preference only: the
// choice is stored per browser and never changes tenant market, currency or
// commercial configuration.
import { t, useLocale, SUPPORTED_LOCALES, type Locale } from '../i18n';

// Each language names itself so the switcher stays readable whatever the
// current UI language is.
const LOCALE_NAMES: Record<Locale, string> = {
  en: 'English',
  cs: 'Čeština',
};

export function LocaleSwitcher() {
  const { locale, setLocale } = useLocale();
  return (
    <select
      className="locale-switcher"
      aria-label={t('common.language')}
      value={locale}
      onChange={(e) => setLocale(e.target.value as Locale)}
    >
      {SUPPORTED_LOCALES.map((code) => (
        <option key={code} value={code}>
          {LOCALE_NAMES[code]}
        </option>
      ))}
    </select>
  );
}
