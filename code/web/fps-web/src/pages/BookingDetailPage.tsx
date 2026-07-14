import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { cancelBooking, confirmUsage, fetchDrawStatus, type BookingListItem, type DrawStatusResult } from '../api/bookings';
import { displayNextDrawRun, displayResourceNoun, displaySlot, humanizeRejectionReason, isSeatsItem, shouldShowNextDraw } from '../displayLabels';
import { StatusBadge } from '@robertvejvoda/fairspot-ui';
import { ModuleBadge } from '../components/ModuleBadge';
import { t, tDynamic, formatDate as formatDateI18n, formatDateTime as formatDateTimeI18n, formatWallClock, type MessageKey } from '../i18n';

// UX008 (#781) — module-aware status meaning: allocated/waitlisted copy names the
// module's resource (spot vs seat) instead of assuming parking.
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

function statusMeaning(booking: BookingListItem): string | undefined {
  const key = STATUS_MEANING_KEYS[booking.status];
  if (!key) return undefined;
  const noun = displayResourceNoun(booking.resourceType);
  return t(key, { noun, nounLower: noun.toLowerCase() });
}

function demandLabel(demandLevel: string): string {
  return tDynamic('bookings.demand', demandLevel, demandLevel);
}

function resourcePlural(booking: BookingListItem): string {
  return t(isSeatsItem(booking) ? 'bookings.resourcePlural.seat' : 'bookings.resourcePlural.spot');
}

function formatDate(s: string): string {
  const [y, m, d] = s.split('-').map(Number);
  return formatDateI18n(new Date(y, m - 1, d), {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  });
}

function formatTime(time: string): string {
  const [h, min] = time.split(':');
  return formatWallClock(parseInt(h, 10), parseInt(min, 10));
}

function formatDateTime(iso: string): string {
  return formatDateTimeI18n(new Date(iso), {
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
  const nounPlural = resourcePlural(booking);
  const availableLabel = t('bookings.detail.availableResources', { resources: nounPlural });

  if (!isPreDraw && booking.status !== 'Allocated' && booking.status !== 'Rejected') return null;

  return (
    <section className="panel" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <span style={{ fontWeight: 700, fontSize: 15, marginBottom: 4 }}>{t('bookings.detail.allocationExplanation')}</span>

      {isPreDraw && (
        <>
          {nextDrawLabel && <Row label={t('bookings.detail.nextDraw')} value={nextDrawLabel} />}
          {draw?.kind === 'ok' && (
            <>
              <Row label={t('bookings.detail.demandSoFar')} value={demandLabel(draw.demandLevel)} />
              <Row label={t('bookings.detail.requestsSoFar')} value={String(draw.requestCount)} />
              {draw.availableSpotCount > 0 && (
                <Row label={availableLabel} value={String(draw.availableSpotCount)} />
              )}
            </>
          )}
          <div style={{ padding: '6px 0', fontSize: 13, color: '#374151' }}>
            {t('bookings.detail.eligibleNote')}
          </div>
        </>
      )}

      {booking.status === 'Allocated' && (
        <>
          {isCompleted && draw.kind === 'ok' && draw.completedAt && (
            <Row label={t('bookings.detail.drawCompleted')} value={formatDateTime(draw.completedAt)} />
          )}
          {draw?.kind === 'ok' && (
            <>
              <Row label={t('bookings.detail.demand')} value={demandLabel(draw.demandLevel)} />
              <Row label={t('bookings.detail.requests')} value={String(draw.requestCount)} />
              {draw.availableSpotCount > 0 && (
                <Row label={availableLabel} value={String(draw.availableSpotCount)} />
              )}
            </>
          )}
          <div style={{ padding: '6px 0', fontSize: 13, color: '#15803d', fontWeight: 500 }}>
            {t('bookings.detail.resultAllocated')}
          </div>
          <div style={{ fontSize: 13, color: '#374151' }}>
            {isSeatsItem(booking)
              ? t('bookings.detail.matchedSeat')
              : t('bookings.detail.matchedSpot')}
          </div>
        </>
      )}

      {booking.status === 'Rejected' && (
        <>
          {isDrawCapacityRejection && draw?.kind === 'ok' && draw.completedAt && (
            <Row label={t('bookings.detail.drawCompleted')} value={formatDateTime(draw.completedAt)} />
          )}
          {isDrawCapacityRejection && draw?.kind === 'ok' && (
            <>
              <Row label={t('bookings.detail.demand')} value={demandLabel(draw.demandLevel)} />
              <Row label={t('bookings.detail.requests')} value={String(draw.requestCount)} />
              {draw.availableSpotCount > 0 && (
                <Row label={availableLabel} value={String(draw.availableSpotCount)} />
              )}
            </>
          )}
          <div style={{ padding: '6px 0', fontSize: 13, color: '#b91c1c', fontWeight: 500 }}>
            {t('bookings.detail.resultNotAllocated')}
          </div>
          <div style={{ fontSize: 13, color: '#374151' }}>
            {isDrawCapacityRejection
              ? t('bookings.detail.moreEligibleThanAvailable', { resources: nounPlural })
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
      <span style={{ fontWeight: 700, fontSize: 15 }}>{t('bookings.detail.timeline')}</span>
      <TimelineStep label={t('bookings.detail.requestSubmitted')} value={formatDateTime(booking.createdAt)} done />
      {isPreDraw && nextDrawLabel && (
        <TimelineStep label={t('bookings.detail.drawScheduled')} value={nextDrawLabel} done={false} />
      )}
      {drawCompletedAt && (
        <TimelineStep label={t('bookings.detail.drawCompleted')} value={drawCompletedAt} done />
      )}
      {isTerminal && booking.lastStatusChangedAt !== booking.createdAt && (
        <TimelineStep label={t('bookings.detail.lastUpdated')} value={formatDateTime(booking.lastStatusChangedAt)} done />
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
          <p style={{ color: '#6b7280' }}>{t('bookings.detail.notFound')}</p>
          <button onClick={() => navigate('/bookings')} className="btn-primary">{t('bookings.detail.backToReservations')}</button>
        </section>
      </div>
    );
  }

  const slotLabel = displaySlot(booking.allocatedSlotId);
  const nextDrawLabel = shouldShowNextDraw(booking.status) ? displayNextDrawRun(booking.requestedDate) : null;
  const meaning = statusMeaning(booking);

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

  async function handleCancel() {
    if (!booking) return;
    if (!confirm(t('bookings.cancelConfirm'))) return;
    setBusy(true);
    const result = await cancelBooking({ apiBaseUrl, bearerToken }, booking.requestId);
    setBusy(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      showToast(true, t('bookings.toast.cancelled'));
      setBooking({ ...booking, status: 'Cancelled', nextAction: '' });
    } else {
      showToast(false, 'message' in result ? result.message : t('bookings.toast.cancelError'));
    }
  }

  async function handleConfirm() {
    if (!booking) return;
    setBusy(true);
    const result = await confirmUsage({ apiBaseUrl, bearerToken }, booking.requestId);
    setBusy(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      showToast(true, result.data.wasAlreadyConfirmed ? t('bookings.toast.usageAlready') : t('bookings.toast.usageConfirmed'));
      setBooking({ ...booking, status: 'UsageConfirmed', nextAction: '' });
    } else {
      showToast(false, 'message' in result ? result.message : t('bookings.toast.confirmError'));
    }
  }

  return (
    <div className="page-stack">
      <section className="page-hero">
        {/* The page hero is brand-green; the back link must stay readable on it. */}
        <button
          onClick={() => navigate('/bookings')}
          style={{ background: 'none', border: 'none', color: 'rgba(255,255,255,0.92)', cursor: 'pointer', fontSize: 14, padding: 0, marginBottom: 8 }}
        >
          ← {t('nav.myReservations')}
        </button>
        <h2 style={{ margin: 0 }}>{formatDate(booking.requestedDate)}</h2>
      </section>

      <section className="panel" style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
          <span style={{ fontWeight: 700, fontSize: 15 }}>{t('bookings.detail.requestStatus')}</span>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            {isSeatsItem(booking) && <ModuleBadge resourceType={booking.resourceType} />}
            <StatusBadge status={booking.status} />
          </span>
        </div>
        {meaning && (
          <p style={{ margin: '0 0 8px', fontSize: 13, color: '#374151' }}>{meaning}</p>
        )}
        <Row label={t('bookings.detail.time')} value={`${formatTime(booking.timeSlotStart)} – ${formatTime(booking.timeSlotEnd)}`} />
        {slotLabel && <Row label={displayResourceNoun(booking.resourceType)} value={slotLabel} />}
        {booking.reason && <Row label={t('bookings.detail.note')} value={booking.reason} />}
        <Row label={t('bookings.detail.submitted')} value={formatDateTime(booking.createdAt)} />
        <Row label={t('bookings.detail.lastUpdated')} value={formatDateTime(booking.lastStatusChangedAt)} />
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
              {t('bookings.action.cancelRequest')}
            </button>
          )}
          {booking.nextAction === 'confirmUsage' && (
            <button onClick={handleConfirm} disabled={busy} style={actionBtn('#15803d')}>
              {t('bookings.action.confirmUsage')}
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
