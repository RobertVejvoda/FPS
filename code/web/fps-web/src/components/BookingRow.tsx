import { useState } from 'react';
import type { BookingListItem } from '../api/bookings';
import { displayBookingStatus, displayLocation, displayNextDrawRun, displaySlot, shouldShowNextDraw } from '../displayLabels';
import { StatusBadge } from '@robertvejvoda/fairspot-ui';
import { t, formatDate as formatDateI18n, formatWallClock, type MessageKey } from '../i18n';

const STATUS_MEANING_KEYS: Record<string, MessageKey> = {
  Submitted: 'bookings.statusMeaning.Submitted',
  Pending: 'bookings.statusMeaning.Pending',
  Allocated: 'bookings.statusMeaning.Allocated',
  Rejected: 'bookings.statusMeaning.Rejected',
  Cancelled: 'bookings.statusMeaning.Cancelled',
  Expired: 'bookings.statusMeaning.Expired',
  Waitlisted: 'bookings.statusMeaning.Waitlisted',
  UsageConfirmed: 'bookings.statusMeaning.UsageConfirmed',
  NoShow: 'bookings.statusMeaning.NoShow',
};

function statusMeaning(status: string): string | undefined {
  const key = STATUS_MEANING_KEYS[status];
  // BookingRow has no resourceType-aware noun (unlike BookingDetailPage); it
  // always renders the generic parking wording, matching its prior behavior.
  return key ? t(key, { noun: t('labels.resourceNoun.spot'), nounLower: t('labels.resourceNoun.spot').toLowerCase() }) : undefined;
}

function formatDate(s: string): string {
  const [y, m, d] = s.split('-').map(Number);
  return formatDateI18n(new Date(y, m - 1, d), { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });
}

function formatTime(time: string): string {
  const [h, min] = time.split(':');
  return formatWallClock(parseInt(h, 10), parseInt(min, 10));
}

type Props = {
  booking: BookingListItem;
  onCancel?: () => void;
  onConfirmUsage?: () => void;
  onNavigate?: () => void;
  busy?: boolean;
};

export function BookingRow({ booking, onCancel, onConfirmUsage, onNavigate, busy }: Props) {
  const [expanded, setExpanded] = useState(false);
  const meaning = statusMeaning(booking.status);
  const locationLabel = displayLocation(booking.locationId);
  const slotLabel = displaySlot(booking.allocatedSlotId);
  const nextDrawLabel = shouldShowNextDraw(booking.status) ? displayNextDrawRun(booking.requestedDate) : null;

  return (
    <div className="panel" style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <div
        onClick={onNavigate}
        style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8, cursor: onNavigate ? 'pointer' : undefined }}
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <span style={{ fontWeight: 600, fontSize: 15 }}>{formatDate(booking.requestedDate)}</span>
          <span style={{ color: '#6b7280', fontSize: 13 }}>
            {formatTime(booking.timeSlotStart)} – {formatTime(booking.timeSlotEnd)}
            {locationLabel ? ` · ${locationLabel}` : ''}
          </span>
        </div>
        <StatusBadge status={booking.status} label={displayBookingStatus(booking.status)} />
      </div>

      {booking.reason ? (
        <p style={{ margin: 0, fontSize: 13, color: '#374151' }}>{booking.reason}</p>
      ) : null}

      {slotLabel ? (
        <p style={{ margin: 0, fontSize: 13, color: '#6b7280' }}>{t('bookings.tile.spotLabel', { slot: slotLabel })}</p>
      ) : null}

      {nextDrawLabel ? (
        <p style={{ margin: 0, fontSize: 13, color: '#1d4ed8', fontWeight: 600 }}>
          {t('bookings.rowOutcome.nextDraw', { next: nextDrawLabel })}
        </p>
      ) : null}

      <div style={{ display: 'flex', gap: 8, alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap' }}>
        <div>
          {meaning ? (
            <button
              onClick={() => setExpanded((e) => !e)}
              style={{ background: 'none', border: 'none', padding: 0, fontSize: 12, color: 'var(--brand-primary)', cursor: 'pointer', textDecoration: 'underline' }}
            >
              {expanded ? t('bookingRow.hideDetail') : t('bookingRow.whatDoesThisMean')}
            </button>
          ) : null}
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginLeft: 'auto', flexWrap: 'wrap' }}>
          {booking.nextAction === 'cancel' && onCancel ? (
            <button onClick={onCancel} disabled={busy} style={actionStyle('#b91c1c')}>{t('bookings.action.cancelRequest')}</button>
          ) : null}
          {booking.nextAction === 'confirmUsage' && onConfirmUsage ? (
            <button onClick={onConfirmUsage} disabled={busy} style={actionStyle('#15803d')}>{t('bookings.action.confirmUsage')}</button>
          ) : null}
        </div>
      </div>

      {expanded && meaning ? (
        <p style={{ margin: 0, fontSize: 13, color: '#374151', background: '#f9fafb', borderRadius: 6, padding: '8px 12px' }}>
          {meaning}
        </p>
      ) : null}
    </div>
  );
}

function actionStyle(bg: string): React.CSSProperties {
  return { background: bg, color: '#fff', border: 'none', borderRadius: 6, padding: '6px 14px', fontSize: 13, fontWeight: 600, cursor: 'pointer' };
}
