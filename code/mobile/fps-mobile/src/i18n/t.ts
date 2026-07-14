// LOC001 (#744) — message lookup with {param} interpolation and plural support.
//
// English is the source catalog: every key must exist in `en`, and each
// domain file type-checks its `cs` object against its own `en` keys, so a
// missing Czech translation is a compile error, not a runtime hole. At
// runtime a missing key still falls back to English defensively.
// Mirrors code/web/fps-web/src/i18n/t.ts.
import { getCurrentLocale, intlTag, type Locale } from './locale';
import { messages, type MessageKey } from './messages';

export type MessageParams = Record<string, string | number>;

function interpolate(template: string, params?: MessageParams): string {
  if (!params) return template;
  return template.replace(/\{(\w+)\}/g, (match, name: string) =>
    name in params ? String(params[name]) : match,
  );
}

function lookup(key: string, locale: Locale): string | undefined {
  const localized = (messages[locale] as Record<string, string>)[key];
  if (localized !== undefined) return localized;
  return (messages.en as Record<string, string>)[key];
}

export function t(key: MessageKey, params?: MessageParams): string {
  const template = lookup(key, getCurrentLocale());
  return interpolate(template ?? key, params);
}

// Lookup for dynamic keys (e.g. `labels.rejection.${code}`) where the value
// comes from data and may have no catalog entry. Returns the fallback then.
export function tDynamic(prefix: string, value: string, fallback: string, params?: MessageParams): string {
  const template = lookup(`${prefix}.${value}`, getCurrentLocale());
  return interpolate(template ?? fallback, params);
}

// Plural-aware lookup. Catalogs define `${key}.one` / `.few` / `.many` /
// `.other` variants as the language needs them (Czech uses one/few/other for
// integers); the CLDR category resolves via Intl.PluralRules and falls back
// to `.other`. `{count}` is always available as a parameter.
export function tPlural(baseKey: string, count: number, params?: MessageParams): string {
  const locale = getCurrentLocale();
  const category = new Intl.PluralRules(intlTag(locale)).select(count);
  const template =
    lookup(`${baseKey}.${category}`, locale) ?? lookup(`${baseKey}.other`, locale);
  return interpolate(template ?? baseKey, { count, ...params });
}
