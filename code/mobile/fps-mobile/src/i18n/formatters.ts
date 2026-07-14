// LOC001 (#744) — locale-aware date/time formatting.
//
// Every user-visible date/time must go through these helpers instead of raw
// `toLocale*(undefined, …)` calls: the active UI locale (not the device
// default) drives the format, so Czech screens get Czech month names, day-first
// order and a 24-hour clock. They replace the hand-rolled `h:mm AM/PM`
// helpers that used to be duplicated across screens/components — Czech uses a
// 24-hour clock, so hard-coded AM/PM formatting must go through here instead.
// Mirrors code/web/fps-web/src/i18n/formatters.ts; RN 0.81 / Hermes supports
// the Intl APIs this relies on.
import { intlTag } from './locale';

export function formatDate(date: Date, options?: Intl.DateTimeFormatOptions): string {
  return date.toLocaleDateString(intlTag(), options ?? { dateStyle: 'medium' });
}

export function formatDateTime(date: Date, options?: Intl.DateTimeFormatOptions): string {
  return date.toLocaleString(intlTag(), options ?? { dateStyle: 'medium', timeStyle: 'short' });
}

export function formatTime(date: Date, options?: Intl.DateTimeFormatOptions): string {
  return date.toLocaleTimeString(intlTag(), options ?? { hour: '2-digit', minute: '2-digit' });
}

// Formats an HH:mm wall-clock pair without a Date, respecting the locale's
// clock convention (replaces the old hand-built `h:mm AM/PM` helper).
export function formatWallClock(hour: number, minute: number): string {
  const probe = new Date(2000, 0, 1, hour, minute);
  return formatTime(probe);
}

export function formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
  return value.toLocaleString(intlTag(), options);
}
