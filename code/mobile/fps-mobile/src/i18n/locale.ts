// LOC001 (#744) — supported UI locales and the module-level locale store.
//
// The store lives outside React so plain helpers (displayLabels, formatters)
// can read the active locale without threading it through every call site.
// LocaleProvider is the only writer; every write triggers a React re-render
// through useSyncExternalStore, so helper output stays in sync with the UI.
// Mirrors code/web/fps-web/src/i18n/locale.ts, adapted for React Native
// (AsyncStorage instead of window.localStorage — see LocaleContext.tsx).
export type Locale = 'en' | 'cs';

export const SUPPORTED_LOCALES: Locale[] = ['en', 'cs'];

// BCP 47 tags handed to Intl.* APIs. The UI language codes stay short ('cs')
// because catalog keys and stored preferences use bare language codes.
const INTL_TAGS: Record<Locale, string> = { en: 'en', cs: 'cs-CZ' };

export const LOCALE_STORAGE_KEY = 'fps.locale';

let currentLocale: Locale = 'en';
const listeners = new Set<() => void>();

export function getCurrentLocale(): Locale {
  return currentLocale;
}

export function intlTag(locale: Locale = currentLocale): string {
  return INTL_TAGS[locale];
}

export function setCurrentLocale(locale: Locale): void {
  if (locale === currentLocale) return;
  currentLocale = locale;
  for (const listener of listeners) listener();
}

export function subscribeToLocale(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

// Normalizes any BCP 47-ish tag ('cs-CZ', 'cs', 'en-US') to a supported UI
// locale, or null when the language isn't offered.
export function toSupportedLocale(tag: string | null | undefined): Locale | null {
  if (!tag) return null;
  const language = tag.trim().toLowerCase().split(/[-_]/)[0];
  return (SUPPORTED_LOCALES as string[]).includes(language) ? (language as Locale) : null;
}
