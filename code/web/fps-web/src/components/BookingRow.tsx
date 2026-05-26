import { useState } from 'react';
import type { BookingListItem } from '../api/bookings';
import { displayLocation, displayNextDrawRun, displaySlot, shouldShowNextDraw } from '../displayLabels';
import { StatusBadge } from './StatusBadge';

const STATUS_MEANING: Record<string, string> = {
  Submitted: 'Waiting for allocation',
  Pending: 'Waiting for the scheduled Draw',
  Allocated: 'Spot allocated',
  Rejected: 'Request not fulfilled',
  Cancelled: 'Cancelled',
  Expired: 'Time slot has passed',
  Waitlisted: 'Waiting for a released slot',
  UsageConfirmed: 'Usage confirmed',
  NoShow: 'No-show recorded',
};

function formatDate(s: string): string {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });
}

function formatTime(t: string): string {
  const [h, min] = t.split(':');
  const hour = parseInt(h, 10);
  return `${hour % 12 || 12}:${min.padStart(2, '0')} ${hour >= 12 ? 'PM' : 'AM'}`;
}

type Props = {
  booking: BookingListItem;
  onCancel?: () => void;
  onConfirmUsage?: () => void;
  busy?: boolean;
};

export function BookingRow({ booking, onCancel, onConfirmUsage, busy }: Props) {
  const [expanded, setExpanded] = useState(false);
  const meaning = STATUS_MEANING[booking.status];
  const locationLabel = displayLocation(booking.locationId);
  const slotLabel = displaySlot(booking.allocatedSlotId);
  const nextDrawLabel = shouldShowNextDraw(booking.status) ? displayNextDrawRun(booking.requestedDate) : null;

  return (
    <div className="panel" style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <span style={{ fontWeight: 600, fontSize: 15 }}>{formatDate(booking.requestedDate)}</span>
          <span style={{ color: '#6b7280', fontSize: 13 }}>
            {formatTime(booking.timeSlotStart)} – {formatTime(booking.timeSlotEnd)}
            {locationLabel ? ` · ${locationLabel}` : ''}
          </span>
        </div>
        <StatusBadge status={booking.status} />
      </div>

      {booking.reason ? (
        <p style={{ margin: 0, fontSize: 13, color: '#374151' }}>{booking.reason}</p>
      ) : null}

      {slotLabel ? (
        <p style={{ margin: 0, fontSize: 13, color: '#6b7280' }}>Slot: {slotLabel}</p>
      ) : null}

      {nextDrawLabel ? (
        <p style={{ margin: 0, fontSize: 13, color: '#1d4ed8', fontWeight: 600 }}>
          Next Draw: {nextDrawLabel}
        </p>
      ) : null}

      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        {meaning ? (
          <button
            onClick={() => setExpanded((e) => !e)}
            style={{ background: 'none', border: 'none', padding: 0, fontSize: 12, color: 'var(--brand-primary)', cursor: 'pointer', textDecoration: 'underline' }}
          >
            {expanded ? 'Hide detail' : 'What does this mean?'}
          </button>
        ) : null}
        {booking.nextAction === 'cancel' && onCancel ? (
          <button onClick={onCancel} disabled={busy} style={actionStyle('#b91c1c')}>Cancel request</button>
        ) : null}
        {booking.nextAction === 'confirmUsage' && onConfirmUsage ? (
          <button onClick={onConfirmUsage} disabled={busy} style={actionStyle('#15803d')}>Confirm usage</button>
        ) : null}
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
