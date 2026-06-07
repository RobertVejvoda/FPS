import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { cancelBooking, confirmUsage, fetchDrawStatus, type BookingListItem, type DrawStatusResult } from '../api/bookings';
import { displayNextDrawRun, displaySlot, humanizeRejectionReason, shouldShowNextDraw } from '../displayLabels';
import { StatusBadge } from '../components/StatusBadge';

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

const DEMAND_LABEL: Record<string, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
};

function formatDate(s: string): string {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  });
}

function formatTime(t: string): string {
  const [h, min] = t.split(':');
  const hour = parseInt(h, 10);
  return `${hour % 12 || 12}:${min.padStart(2, '0')} ${hour >= 12 ? 'PM' : 'AM'}`;
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    month: 'short', day: 'numeric', year: 'numeric',
    hour: 'numeric', minute: '2-digit',
  });
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 16, padding: '6px 0', borderBottom: '1px solid #f3f4f6' }}>
      <span style={{ fontSize: 13, color: '#6b7280', flexShrink: 0 }}>{label}</span>
      <span style={{ fontSize: 14, fontWeight: 500, color: '#111827', textAlign: 'right' }}>{value}</span>
    </div>
  );
}

function AllocationExplanation({ booking, draw, nextDrawLabel }: {
  booking: BookingListItem;
  draw: DrawStatusResult | null;
  nextDrawLabel: string | null;
}) {
  const isPreDraw = shouldShowNextDraw(booking.status);
  const isCompleted = draw?.kind === 'ok' && draw.status === 'Completed';
  const isDrawCapacityRejection = booking.reasonCode === 'DrawNotSelected' || (!booking.reasonCode && isCompleted);

  if (!isPreDraw && booking.status !== 'Allocated' && booking.status !== 'Rejected') return null;

  return (
    <section className="panel" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <span style={{ fontWeight: 700, fontSize: 15, marginBottom: 4 }}>Allocation explanation</span>

      {isPreDraw && (
        <>
          {nextDrawLabel && <Row label="Next draw" value={nextDrawLabel} />}
          {draw?.kind === 'ok' && (
            <>
              <Row label="Demand so far" value={DEMAND_LABEL[draw.demandLevel] ?? draw.demandLevel} />
              <Row label="Requests so far" value={String(draw.requestCount)} />
              {draw.availableSpotCount > 0 && (
                <Row label="Available spots" value={String(draw.availableSpotCount)} />
              )}
            </>
          )}
          <div style={{ padding: '6px 0', fontSize: 13, color: '#374151' }}>
            You are eligible. Final allocation follows eligibility and fairness rules.
          </div>
        </>
      )}

      {booking.status === 'Allocated' && (
        <>
          {isCompleted && draw.kind === 'ok' && draw.completedAt && (
            <Row label="Draw completed" value={formatDateTime(draw.completedAt)} />
          )}
          {draw?.kind === 'ok' && (
            <>
              <Row label="Demand" value={DEMAND_LABEL[draw.demandLevel] ?? draw.demandLevel} />
              <Row label="Requests" value={String(draw.requestCount)} />
              {draw.availableSpotCount > 0 && (
                <Row label="Available spots" value={String(draw.availableSpotCount)} />
              )}
            </>
          )}
          <div style={{ padding: '6px 0', fontSize: 13, color: '#15803d', fontWeight: 500 }}>
            Result: Allocated
          </div>
          <div style={{ fontSize: 13, color: '#374151' }}>
            Your request matched an available parking spot.
          </div>
        </>
      )}

      {booking.status === 'Rejected' && (
        <>
          {isDrawCapacityRejection && draw?.kind === 'ok' && draw.completedAt && (
            <Row label="Draw completed" value={formatDateTime(draw.completedAt)} />
          )}
          {isDrawCapacityRejection && draw?.kind === 'ok' && (
            <>
              <Row label="Demand" value={DEMAND_LABEL[draw.demandLevel] ?? draw.demandLevel} />
              <Row label="Requests" value={String(draw.requestCount)} />
              {draw.availableSpotCount > 0 && (
                <Row label="Available spots" value={String(draw.availableSpotCount)} />
              )}
            </>
          )}
          <div style={{ padding: '6px 0', fontSize: 13, color: '#b91c1c', fontWeight: 500 }}>
            Result: Not allocated
          </div>
          <div style={{ fontSize: 13, color: '#374151' }}>
            {isDrawCapacityRejection
              ? 'More eligible requests than available spots. The draw followed company policy.'
              : humanizeRejectionReason(booking.reasonCode ?? null, booking.reason ?? null)}
          </div>
        </>
      )}
    </section>
  );
}

function TimelineStep({ label, value, done }: { label: string; value?: string; done: boolean }) {
  return (
    <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
      <div style={{
        width: 20, height: 20, borderRadius: '50%', flexShrink: 0, marginTop: 1,
        background: done ? '#1d4ed8' : 'transparent',
        border: `2px solid ${done ? '#1d4ed8' : '#d1d5db'}`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        {done && <span style={{ color: '#fff', fontSize: 11, fontWeight: 700 }}>✓</span>}
      </div>
      <div>
        <div style={{ fontSize: 14, fontWeight: 500, color: '#111827' }}>{label}</div>
        {value && <div style={{ fontSize: 12, color: '#6b7280', marginTop: 2 }}>{value}</div>}
      </div>
    </div>
  );
}

function Timeline({ booking, draw, nextDrawLabel }: {
  booking: BookingListItem;
  draw: DrawStatusResult | null;
  nextDrawLabel: string | null;
}) {
  const isPreDraw = shouldShowNextDraw(booking.status);
  const isCompleted = draw?.kind === 'ok' && draw.status === 'Completed';
  const drawCompletedAt = isCompleted && draw.kind === 'ok' && draw.completedAt ? formatDateTime(draw.completedAt) : null;
  const isTerminal = ['Allocated', 'Rejected', 'Cancelled', 'Expired', 'UsageConfirmed', 'NoShow'].includes(booking.status);

  return (
    <section className="panel" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <span style={{ fontWeight: 700, fontSize: 15 }}>Timeline</span>
      <TimelineStep label="Request submitted" value={formatDateTime(booking.createdAt)} done />
      {isPreDraw && nextDrawLabel && (
        <TimelineStep label="Draw scheduled" value={nextDrawLabel} done={false} />
      )}
      {drawCompletedAt && (
        <TimelineStep label="Draw completed" value={drawCompletedAt} done />
      )}
      {isTerminal && booking.lastStatusChangedAt !== booking.createdAt && (
        <TimelineStep label="Last updated" value={formatDateTime(booking.lastStatusChangedAt)} done />
      )}
    </section>
  );
}

export function BookingDetailPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const [booking, setBooking] = useState<BookingListItem | null>(
    (location.state as BookingListItem) ?? null,
  );
  const [busy, setBusy] = useState(false);
  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);
  const [draw, setDraw] = useState<DrawStatusResult | null>(null);

  useEffect(() => {
    if (!booking) return;
    const needsDraw = shouldShowNextDraw(booking.status)
      || booking.status === 'Allocated'
      || booking.status === 'Rejected';
    if (!needsDraw || !booking.locationId) return;
    fetchDrawStatus({ apiBaseUrl, bearerToken }, {
      date: booking.requestedDate,
      locationId: booking.locationId,
      timeSlotStart: booking.timeSlotStart,
      timeSlotEnd: booking.timeSlotEnd,
    }).then(setDraw);
  }, [booking?.status, booking?.requestedDate, booking?.locationId, booking?.timeSlotStart, booking?.timeSlotEnd, apiBaseUrl, bearerToken]);

  if (!booking) {
    return (
      <div className="page-stack">
        <section className="panel">
          <p style={{ color: '#6b7280' }}>Request not found.</p>
          <button onClick={() => navigate('/bookings')} className="btn-primary">Back to My Spots</button>
        </section>
      </div>
    );
  }

  const slotLabel = displaySlot(booking.allocatedSlotId);
  const nextDrawLabel = shouldShowNextDraw(booking.status) ? displayNextDrawRun(booking.requestedDate) : null;
  const meaning = STATUS_MEANING[booking.status];

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

  async function handleCancel() {
    if (!booking) return;
    if (!confirm('Cancel this spot request?')) return;
    setBusy(true);
    const result = await cancelBooking({ apiBaseUrl, bearerToken }, booking.requestId);
    setBusy(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      showToast(true, 'Request cancelled.');
      setBooking({ ...booking, status: 'Cancelled', nextAction: '' });
    } else {
      showToast(false, 'message' in result ? result.message : 'Could not cancel this request.');
    }
  }

  async function handleConfirm() {
    if (!booking) return;
    setBusy(true);
    const result = await confirmUsage({ apiBaseUrl, bearerToken }, booking.requestId);
    setBusy(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      showToast(true, result.data.wasAlreadyConfirmed ? 'Usage was already recorded.' : 'Usage confirmed.');
      setBooking({ ...booking, status: 'UsageConfirmed', nextAction: '' });
    } else {
      showToast(false, 'message' in result ? result.message : 'Could not confirm usage.');
    }
  }

  return (
    <div className="page-stack">
      <section className="page-hero">
        <button
          onClick={() => navigate('/bookings')}
          style={{ background: 'none', border: 'none', color: 'var(--brand-primary)', cursor: 'pointer', fontSize: 14, padding: 0, marginBottom: 8 }}
        >
          ← My Spots
        </button>
        <h2 style={{ margin: 0 }}>{formatDate(booking.requestedDate)}</h2>
      </section>

      <section className="panel" style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
          <span style={{ fontWeight: 700, fontSize: 15 }}>Request status</span>
          <StatusBadge status={booking.status} />
        </div>
        {meaning && (
          <p style={{ margin: '0 0 8px', fontSize: 13, color: '#374151' }}>{meaning}</p>
        )}
        <Row label="Time" value={`${formatTime(booking.timeSlotStart)} – ${formatTime(booking.timeSlotEnd)}`} />
        {slotLabel && <Row label="Spot" value={slotLabel} />}
        {booking.reason && <Row label="Note" value={booking.reason} />}
        <Row label="Submitted" value={formatDateTime(booking.createdAt)} />
        <Row label="Last updated" value={formatDateTime(booking.lastStatusChangedAt)} />
      </section>

      <AllocationExplanation booking={booking} draw={draw} nextDrawLabel={nextDrawLabel} />

      <Timeline booking={booking} draw={draw} nextDrawLabel={nextDrawLabel} />

      {toast && (
        <div style={{ padding: '10px 16px', borderRadius: 8, background: toast.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`, color: toast.ok ? '#166534' : '#b91c1c', fontSize: 13, fontWeight: 500 }}>
          {toast.text}
        </div>
      )}

      {(booking.nextAction === 'cancel' || booking.nextAction === 'confirmUsage') && (
        <section className="panel" style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
          {booking.nextAction === 'cancel' && (
            <button onClick={handleCancel} disabled={busy} style={actionBtn('#b91c1c')}>
              Cancel request
            </button>
          )}
          {booking.nextAction === 'confirmUsage' && (
            <button onClick={handleConfirm} disabled={busy} style={actionBtn('#15803d')}>
              Confirm usage
            </button>
          )}
        </section>
      )}
    </div>
  );
}

function actionBtn(bg: string): React.CSSProperties {
  return { background: bg, color: '#fff', border: 'none', borderRadius: 8, padding: '10px 20px', fontSize: 14, fontWeight: 600, cursor: 'pointer' };
}
