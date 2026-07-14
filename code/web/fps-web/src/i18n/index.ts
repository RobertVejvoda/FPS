// LOC001 (#744) — public i18n surface. Components import from './i18n' (or
// relative equivalent) and use t()/tPlural()/tDynamic() for copy plus the
// format* helpers for locale-aware dates, times and numbers.
export { t, tDynamic, tPlural, type MessageParams } from './t';
export { formatDate, formatDateTime, formatTime, formatWallClock, formatNumber } from './formatters';
export { LocaleProvider, useLocale } from './LocaleContext';
export { SUPPORTED_LOCALES, getCurrentLocale, intlTag, toSupportedLocale, type Locale } from './locale';
export type { MessageKey } from './messages';
