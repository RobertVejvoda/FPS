// LOC001 (#744) — app shell, navigation, and generic shared copy used across
// several screens. Catalog convention (same in every domain file): `en` is
// the source of truth, `cs` is typed against `en`'s keys so a missing
// translation fails `npm run typecheck`. Czech copy uses formal address
// (vykání). Mirrors code/web/fps-web/src/i18n/messages/common.ts where the
// concepts overlap (nav labels, relative day names).
import type { Catalog } from '../catalog';

const en = {
  'common.loading': 'Loading…',
  'common.notAvailable': '—',
  'common.yes': 'Yes',
  'common.no': 'No',
  'common.retry': 'Retry',
  'common.refresh': 'Refresh',
  'common.cancel': 'Cancel',
  'common.checkConnection': 'Please check your connection and try again.',
  'common.today': 'Today',
  'common.tomorrow': 'Tomorrow',

  // Tab bar + stack titles (app/(tabs)/_layout.tsx, app/_layout.tsx)
  'nav.home': 'Home',
  'nav.reservations': 'Reservations',
  'nav.request': 'Request',
  'nav.alerts': 'Alerts',
  'nav.more': 'More',
  'nav.access': 'Access',
  'nav.bookingDetail': 'Booking Detail',
  'nav.back': 'Back',
} as const;

const cs: Catalog<keyof typeof en> = {
  'common.loading': 'Načítání…',
  'common.notAvailable': '—',
  'common.yes': 'Ano',
  'common.no': 'Ne',
  'common.retry': 'Zkusit znovu',
  'common.refresh': 'Obnovit',
  'common.cancel': 'Zrušit',
  'common.checkConnection': 'Zkontrolujte prosím připojení a zkuste to znovu.',
  'common.today': 'Dnes',
  'common.tomorrow': 'Zítra',

  'nav.home': 'Domů',
  'nav.reservations': 'Rezervace',
  'nav.request': 'Žádost',
  'nav.alerts': 'Upozornění',
  'nav.more': 'Více',
  'nav.access': 'Přístup',
  'nav.bookingDetail': 'Detail rezervace',
  'nav.back': 'Zpět',
};

export const commonMessages = { en, cs };
