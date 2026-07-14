// LOC001 (#744) — merged message catalogs.
//
// Each domain file owns its keys (namespaced by prefix) and type-checks its
// Czech catalog against its English keys. This index only composes them; new
// domains register here once. Mirrors code/web/fps-web/src/i18n/messages/index.ts.
import { commonMessages } from './common';
import { sessionMessages } from './session';
import { bookingMessages } from './booking';
import { notificationsMessages } from './notifications';
import { moreMessages } from './more';

export const messages = {
  en: {
    ...commonMessages.en,
    ...sessionMessages.en,
    ...bookingMessages.en,
    ...notificationsMessages.en,
    ...moreMessages.en,
  },
  cs: {
    ...commonMessages.cs,
    ...sessionMessages.cs,
    ...bookingMessages.cs,
    ...notificationsMessages.cs,
    ...moreMessages.cs,
  },
} as const;

export type MessageKey = keyof typeof messages.en;
