// LOC001 (#744) — in-app notifications tab + NotificationCard copy.
// Mirrors code/web/fps-web/src/i18n/messages/notifications.ts.
import type { Catalog } from '../catalog';

const en = {
  'notifications.filter.all': 'All',
  'notifications.filter.unread': 'Unread',
  'notifications.loading': 'Loading notifications…',
  'notifications.signInPrompt': 'Sign in to see your parking notifications.',
  'notifications.cannotLoad': 'Cannot load alerts',
  'notifications.empty.unreadTitle': 'No unread notifications',
  'notifications.empty.title': 'No notifications yet',
  'notifications.empty.unreadMessage': 'All caught up. Switch to All to see your history.',
  'notifications.empty.message': 'Booking and allocation updates will appear here.',
  'notifications.markAsRead': 'Mark as read',

  // NotificationCard type badges — keyed by the wire notificationType value.
  'labels.notificationType.RequestSubmitted': 'Request submitted',
  'labels.notificationType.RequestRejected': 'Request rejected',
  'labels.notificationType.SlotAllocated': 'Slot allocated',
  'labels.notificationType.SlotAllocatedByReallocation': 'Slot reallocated',
  'labels.notificationType.RequestCancelledBeforeAllocation': 'Request cancelled',
  'labels.notificationType.AllocatedReservationCancelled': 'Reservation cancelled',
  'labels.notificationType.LateCancellationPenaltyApplied': 'Late cancellation penalty',
  'labels.notificationType.NoShowRecorded': 'No-show recorded',
  'labels.notificationType.NoShowPenaltyApplied': 'No-show penalty',
  'labels.notificationType.ManualCorrection': 'Manual correction',
  'labels.notificationType.DrawCompleted': 'Draw completed',
} as const;

const cs: Catalog<keyof typeof en> = {
  'notifications.filter.all': 'Vše',
  'notifications.filter.unread': 'Nepřečtené',
  'notifications.loading': 'Načítání oznámení…',
  'notifications.signInPrompt': 'Přihlaste se a zobrazte oznámení o parkování.',
  'notifications.cannotLoad': 'Upozornění se nepodařilo načíst',
  'notifications.empty.unreadTitle': 'Žádná nepřečtená oznámení',
  'notifications.empty.title': 'Zatím žádná oznámení',
  'notifications.empty.unreadMessage': 'Vše je přečteno. Přepněte na Vše a zobrazte historii.',
  'notifications.empty.message': 'Zde se zobrazí aktualizace rezervací a přidělení.',
  'notifications.markAsRead': 'Označit jako přečtené',

  'labels.notificationType.RequestSubmitted': 'Žádost odeslána',
  'labels.notificationType.RequestRejected': 'Žádost zamítnuta',
  'labels.notificationType.SlotAllocated': 'Místo přiděleno',
  'labels.notificationType.SlotAllocatedByReallocation': 'Místo přiděleno přerozdělením',
  'labels.notificationType.RequestCancelledBeforeAllocation': 'Žádost zrušena',
  'labels.notificationType.AllocatedReservationCancelled': 'Rezervace zrušena',
  'labels.notificationType.LateCancellationPenaltyApplied': 'Penalizace za pozdní zrušení',
  'labels.notificationType.NoShowRecorded': 'Zaznamenáno nevyužití rezervace',
  'labels.notificationType.NoShowPenaltyApplied': 'Penalizace za nevyužití rezervace',
  'labels.notificationType.ManualCorrection': 'Ruční korekce',
  'labels.notificationType.DrawCompleted': 'Losování dokončeno',
};

export const notificationsMessages = { en, cs };
