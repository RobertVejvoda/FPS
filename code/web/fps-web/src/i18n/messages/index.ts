// LOC001 (#744) — merged message catalogs.
//
// Each domain file owns its keys (namespaced by prefix) and type-checks its
// Czech catalog against its English keys. This index only composes them; new
// domains register here once.
import { commonMessages } from './common';
import { labelsMessages } from './labels';
import { sessionMessages } from './session';
import { bookingsMessages } from './bookings';
import { notificationsMessages } from './notifications';
import { profileMessages } from './profile';
import { hrMessages } from './hr';
import { adminMessages } from './admin';
import { auditMessages } from './audit';
import { reportingMessages } from './reporting';

export const messages = {
  en: {
    ...commonMessages.en,
    ...labelsMessages.en,
    ...sessionMessages.en,
    ...bookingsMessages.en,
    ...notificationsMessages.en,
    ...profileMessages.en,
    ...hrMessages.en,
    ...adminMessages.en,
    ...auditMessages.en,
    ...reportingMessages.en,
  },
  cs: {
    ...commonMessages.cs,
    ...labelsMessages.cs,
    ...sessionMessages.cs,
    ...bookingsMessages.cs,
    ...notificationsMessages.cs,
    ...profileMessages.cs,
    ...hrMessages.cs,
    ...adminMessages.cs,
    ...auditMessages.cs,
    ...reportingMessages.cs,
  },
} as const;

export type MessageKey = keyof typeof messages.en;
