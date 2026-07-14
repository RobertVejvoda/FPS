import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, cancelBooking, confirmUsage, fetchDrawStatus, type BookingListItem, type DrawStatusResult } from '../api/bookings';
import { displayBookingStatus, displayCannotRequestReason, displayResourceNoun, displayScheduleMessage, displaySlot, displayNextDrawRun, formatCutOffAt, humanizeRejectionReason, isSeatsItem, shouldShowNextDraw } from '../displayLabels';
import { StatusBadge } from '@robertvejvoda/fairspot-ui';
import { NotificationBanner } from '../components/NotificationBanner';
import { ModuleBadge } from '../components/ModuleBadge';
import { addCalendarDays, fromLocalDateString, labelRelativeWorkday, labelWeekdayDate, nextWorkdayOptions, toLocalDateString } from '../dateOptions';
import { useTenantDateContext } from '../hooks/useTenantDateBase';
import { useTenantModules } from '../tenant/TenantModulesContext';
import { t, formatDate, formatWallClock } from '../i18n';

const FALLBACK_LOCATION_ID = 'Prague';
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';
// How many past records the landing list mixes in under "Earlier" before
// pointing at the full history page.
const RECENT_WINDOW = 5;

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: BookingListItem[]; totalCount: number }
  | { kind: 'error'; message: string };

type ModuleFilter = 'all' | 'Parking' | 'Seats';

export function BookingsPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const { hasSeats, multiModule } = useTenantModules();
  const [upcoming, setUpcoming] = useState<LoadState>({ kind: 'loading' });
  const [recent, setRecent] = useState<LoadState>({ kind: 'loading' });
  const [moduleFilter, setModuleFilter] = useState<ModuleFilter>('all');
  const [busyId, setBusyId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);
  const [drawStatuses, setDrawStatuses] = useState<(DrawStatusResult | null)[]>([]);
  const [drawStatusesLoading, setDrawStatusesLoading] = useState(true);
  const { dateBase, simulationActive } = useTenantDateContext();
  const days = useMemo(() => nextWorkdayOptions(dateBase, 4, { relativeLabels: !simulationActive }), [dateBase, simulationActive]);
  const todayStr = useMemo(() => toLocalDateString(dateBase), [dateBase]);
  const yesterdayStr = useMemo(() => toLocalDateString(addCalendarDays(fromLocalDateString(todayStr), -1)), [todayStr]);

  const upcomingItems = upcoming.kind === 'ok' ? upcoming.items : [];

  const drawLocationId = upcomingItems.find(i => i.locationId && !isSeatsItem(i))?.locationId ?? FALLBACK_LOCATION_ID;

  const load = useCallback(() => {
    setUpcoming({ kind: 'loading' });
    setRecent({ kind: 'loading' });
    fetchBookings({ apiBaseUrl, bearerToken }, { from: todayStr }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setUpcoming({ kind: 'ok', items: result.items, totalCount: result.totalCount });
      else setUpcoming({ kind: 'error', message: 'message' in result ? result.message : t('bookings.error.loadUpcoming') });
    });
    fetchBookings({ apiBaseUrl, bearerToken }, { to: yesterdayStr }).then((result) => {
      if (result.kind === 'unauthenticated') return; // the upcoming fetch already handles the redirect
      if (result.kind === 'ok') setRecent({ kind: 'ok', items: result.items, totalCount: result.totalCount });
      else setRecent({ kind: 'error', message: 'message' in result ? result.message : t('bookings.error.loadRecent') });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, todayStr, yesterdayStr]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    let cancelled = false;
    setDrawStatusesLoading(true);
    setDrawStatuses(days.map(() => null));
    Promise.all(
      days.map(day => fetchDrawStatus({ apiBaseUrl, bearerToken }, {
        date: day.date,
        locationId: drawLocationId,
        timeSlotStart: WORKDAY_START,
        timeSlotEnd: WORKDAY_END,
      }))
    ).then(results => {
      if (cancelled) return;
      setDrawStatuses(results);
      setDrawStatusesLoading(false);
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, drawLocationId, days]);

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

  async function handleCancel(requestId: string) {
    if (!confirm(t('bookings.cancelConfirm'))) return;
    setBusyId(requestId);
    const result = await cancelBooking({ apiBaseUrl, bearerToken }, requestId);
    setBusyId(null);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') { showToast(true, t('bookings.toast.cancelled')); load(); }
    else showToast(false, 'message' in result ? result.message : t('bookings.toast.cancelError'));
  }

  async function handleConfirm(requestId: string) {
    setBusyId(requestId);
    const result = await confirmUsage({ apiBaseUrl, bearerToken }, requestId);
    setBusyId(null);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      showToast(true, result.data.wasAlreadyConfirmed ? t('bookings.toast.usageAlready') : t('bookings.toast.usageConfirmed'));
      load();
    } else showToast(false, 'message' in result ? result.message : t('bookings.toast.confirmError'));
  }

  const recentItems = recent.kind === 'ok' ? recent.items.slice(0, RECENT_WINDOW) : [];

  // Module filter chips appear once the tenant runs more than one module or the
  // employee's own records already span modules — never for a parking-only tenant.
  const dataSpansModules = useMemo(() => {
    const all = [...upcomingItems, ...recentItems];
    return all.some(isSeatsItem) && all.some(i => !isSeatsItem(i));
  }, [upcomingItems, recentItems]);
  const showModuleFilter = multiModule || dataSpansModules;

  const matchesFilter = useCallback((item: BookingListItem) => {
    if (moduleFilter === 'all') return true;
    return moduleFilter === 'Seats' ? isSeatsItem(item) : !isSeatsItem(item);
  }, [moduleFilter]);

  // Date-first grouping: upcoming dates ascending (today first), then one
  // collapsed "Earlier" group with the most recent past records.
  const dateGroups = useMemo(() => {
    const byDate = new Map<string, BookingListItem[]>();
    for (const item of upcomingItems.filter(matchesFilter)) {
      const bucket = byDate.get(item.requestedDate) ?? [];
      bucket.push(item);
      byDate.set(item.requestedDate, bucket);
    }
    return [...byDate.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([date, items]) => ({
        date,
        label: simulationActive
          ? labelWeekdayDate(fromLocalDateString(date))
          : labelRelativeWorkday(fromLocalDateString(todayStr), fromLocalDateString(date)),
        items: items.sort((a, b) => a.timeSlotStart.localeCompare(b.timeSlotStart)),
      }));
  }, [upcomingItems, matchesFilter, simulationActive, todayStr]);

  const earlierItems = recentItems.filter(matchesFilter);

  const totalCount = (upcoming.kind === 'ok' ? upcoming.totalCount : 0) + (recent.kind === 'ok' ? recent.totalCount : 0);
  const shownCount = dateGroups.reduce((n, g) => n + g.items.length, 0) + earlierItems.length;

  return (
    <div className="page-stack">
      <section className="page-hero">
        <h2>{t('bookings.title')}</h2>
      </section>

      <NotificationBanner />

      {toast && (
        <div style={{ padding: '10px 16px', borderRadius: 8, background: toast.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`, color: toast.ok ? '#166534' : '#b91c1c', fontSize: 13, fontWeight: 500 }}>
          {toast.text}
        </div>
      )}

      {upcoming.kind === 'error' && (
        <div className="panel">
          <p style={{ color: '#b91c1c', margin: '0 0 8px' }}>{upcoming.message}</p>
          <button onClick={load} className="btn-primary">{t('bookings.retry')}</button>
        </div>
      )}

      {/* Day focus cards — parking is the fully wired request module; a seat
          reservation for the day renders as a compact badged row inside the card. */}
      <div className="day-tiles-grid">
        {days.map((day, i) => {
          const date = day.date;
          const booking = upcomingItems.find(b => b.requestedDate === date && !isSeatsItem(b)) ?? null;
          const seatBooking = upcomingItems.find(b => b.requestedDate === date && isSeatsItem(b)) ?? null;
          return (
            <DayTile
              key={day.date}
              label={day.label}
              date={date}
              booking={booking}
              seatBooking={seatBooking}
              drawStatus={drawStatuses[i] ?? null}
              drawLoading={upcoming.kind === 'loading' || drawStatusesLoading}
              busy={busyId === booking?.requestId}
              onCancel={booking?.nextAction === 'cancel' ? () => handleCancel(booking.requestId) : undefined}
              onConfirm={booking?.nextAction === 'confirmUsage' ? () => handleConfirm(booking.requestId) : undefined}
              onRequest={() => navigate(`/bookings/new?date=${date}`)}
              onDetails={booking ? () => navigate(`/bookings/${booking.requestId}`, { state: booking }) : undefined}
              onSeatDetails={seatBooking ? () => navigate(`/bookings/${seatBooking.requestId}`, { state: seatBooking }) : undefined}
            />
          );
        })}
      </div>

      {/* Secondary navigation */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap', fontSize: 13 }}>
        <div style={{ display: 'flex', gap: 16 }}>
          <button onClick={() => navigate('/bookings/new')} className="btn-link">
            {t('bookings.requestAnotherDate')}
          </button>
          {hasSeats && (
            <button onClick={() => navigate('/bookings/new?module=seats')} className="btn-link">
              {t('bookings.requestSeat')}
            </button>
          )}
        </div>
        <button onClick={() => navigate('/bookings/history')} className="btn-link">
          {t('bookings.historyLink')}
        </button>
      </div>

      {/* Date-grouped reservation and request list across enabled modules. */}
      <section className="panel" style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <span style={{ fontWeight: 700, fontSize: 15 }}>{t('bookings.myRequests')}</span>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            {showModuleFilter && (
              <div className="module-filter" role="group" aria-label={t('bookings.filter.ariaLabel')}>
                {(['all', 'Parking', 'Seats'] as const).map(f => (
                  <button
                    key={f}
                    onClick={() => setModuleFilter(f)}
                    className={`module-filter-chip${moduleFilter === f ? ' module-filter-chip-active' : ''}`}
                    aria-pressed={moduleFilter === f}
                  >
                    {f === 'all' ? t('bookings.filter.all') : f === 'Seats' ? t('bookings.filter.seats') : t('bookings.filter.parking')}
                  </button>
                ))}
              </div>
            )}
            {totalCount > 0 && (
              <span style={{ fontSize: 12, color: '#6b7280' }}>{t('bookings.showingCount', { shown: shownCount, total: totalCount })}</span>
            )}
          </div>
        </div>

        {upcoming.kind === 'loading' && <p style={{ color: '#6b7280', margin: 0, fontSize: 13 }}>{t('common.loading')}</p>}
        {upcoming.kind === 'ok' && dateGroups.length === 0 && earlierItems.length === 0 && (
          <p style={{ color: '#6b7280', margin: 0, fontSize: 13 }}>
            {hasSeats ? t('bookings.empty.noRequestsWithSeat') : t('bookings.empty.noRequests')}
          </p>
        )}

        {dateGroups.map(group => (
          <div key={group.date} className="resv-group">
            <div className="resv-group-label">{group.label}</div>
            {group.items.map(item => (
              <ReservationRow
                key={item.requestId}
                item={item}
                showModule={showModuleFilter}
                onOpen={() => navigate(`/bookings/${item.requestId}`, { state: item })}
              />
            ))}
          </div>
        ))}

        {earlierItems.length > 0 && (
          <div className="resv-group">
            <div className="resv-group-label">{t('bookings.earlier')}</div>
            {earlierItems.map(item => (
              <ReservationRow
                key={item.requestId}
                item={item}
                showModule={showModuleFilter}
                onOpen={() => navigate(`/bookings/${item.requestId}`, { state: item })}
              />
            ))}
            <button onClick={() => navigate('/bookings/history')} className="btn-link" style={{ alignSelf: 'flex-start', marginLeft: -12 }}>
              {t('bookings.fullHistory')}
            </button>
          </div>
        )}
        {recent.kind === 'error' && (
          <p style={{ color: '#b91c1c', margin: 0, fontSize: 13 }}>{recent.message}</p>
        )}
      </section>
    </div>
  );
}

// One employee-safe outcome line per row: allocated resource label, next draw
// timing while waiting, or the business reason — never raw ids or internals.
function rowOutcome(item: BookingListItem): string | null {
  const slotLabel = displaySlot(item.allocatedSlotId);
  if (slotLabel) return `${displayResourceNoun(item.resourceType)}: ${slotLabel}`;
  if (shouldShowNextDraw(item.status)) {
    const next = displayNextDrawRun(item.requestedDate);
    return next ? t('bookings.rowOutcome.nextDraw', { next }) : null;
  }
  if (item.status === 'Waitlisted') return t('bookings.rowOutcome.waitingReleased', { noun: displayResourceNoun(item.resourceType).toLowerCase() });
  if (item.status === 'Rejected') return humanizeRejectionReason(item.reasonCode ?? null, item.reason ?? null);
  if (item.status === 'Cancelled') return t('bookings.rowOutcome.cancelled');
  return item.reason ?? null;
}

function formatRowTime(time: string): string {
  const [h, m] = time.split(':');
  return formatWallClock(parseInt(h, 10), parseInt(m, 10));
}

function ReservationRow({ item, showModule, onOpen }: {
  item: BookingListItem;
  showModule: boolean;
  onOpen: () => void;
}) {
  const outcome = rowOutcome(item);
  return (
    <button className="resv-row" onClick={onOpen}>
      {showModule && <ModuleBadge resourceType={item.resourceType} />}
      <StatusBadge status={item.status} label={displayBookingStatus(item.status)} />
      <span className="resv-row-outcome">{outcome ?? '—'}</span>
      <span className="resv-row-time">{formatRowTime(item.timeSlotStart)} – {formatRowTime(item.timeSlotEnd)}</span>
      <span className="resv-row-chevron" aria-hidden="true">›</span>
    </button>
  );
}

function DayTile({ label, date, booking, seatBooking, drawStatus, drawLoading, busy, onCancel, onConfirm, onRequest, onDetails, onSeatDetails }: {
  label: string;
  date: string;
  booking: BookingListItem | null;
  seatBooking: BookingListItem | null;
  drawStatus: DrawStatusResult | null;
  drawLoading?: boolean;
  busy?: boolean;
  onCancel?: () => void;
  onConfirm?: () => void;
  onRequest?: () => void;
  onDetails?: () => void;
  onSeatDetails?: () => void;
}) {
  const scheduleOk = drawStatus?.kind === 'ok' ? drawStatus : null;
  const d = new Date(date + 'T00:00:00');
  const dateLabel = formatDate(d, { month: 'short', day: 'numeric' });

  return (
    <div style={tileStyle}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 4 }}>
        <div>
          <div style={tileDayStyle}>{label}</div>
          <div style={{ fontSize: 11, color: '#9ca3af', marginTop: 1 }}>{dateLabel}</div>
        </div>
        {booking && <StatusBadge status={booking.status} label={displayBookingStatus(booking.status)} />}
      </div>

      {/* Allocated spot */}
      {booking && displaySlot(booking.allocatedSlotId) && (
        <div style={{ fontSize: 13, fontWeight: 600, color: '#374151', marginTop: 6 }}>
          {t('bookings.tile.spotLabel', { slot: displaySlot(booking.allocatedSlotId) ?? '' })}
        </div>
      )}

      {/* Seat reservation for the day — compact badged row, only when one exists. */}
      {seatBooking && (
        <button onClick={onSeatDetails} className="tile-seat-row">
          <ModuleBadge resourceType={seatBooking.resourceType} />
          <StatusBadge status={seatBooking.status} label={displayBookingStatus(seatBooking.status)} />
          {displaySlot(seatBooking.allocatedSlotId) && (
            <span style={{ fontSize: 12, fontWeight: 600, color: '#374151' }}>{displaySlot(seatBooking.allocatedSlotId)}</span>
          )}
        </button>
      )}

      {/* Draw/schedule timing */}
      {drawLoading && <div style={{ fontSize: 11, color: '#9ca3af', marginTop: 6 }}>{t('bookings.tile.loadingSchedule')}</div>}
      {!drawLoading && scheduleOk?.nextDrawAt && (
        <div style={{ fontSize: 11, color: '#6b7280', marginTop: 6 }}>
          {t('bookings.tile.draw', { time: formatCutOffAt(scheduleOk.nextDrawAt, scheduleOk.timeZone) })}
        </div>
      )}
      {!drawLoading && scheduleOk?.cutOffAt && (
        <div style={{ fontSize: 11, color: '#6b7280', marginTop: 2 }}>
          {t('bookings.tile.cutoff', { time: formatCutOffAt(scheduleOk.cutOffAt, scheduleOk.timeZone) })}
        </div>
      )}
      {!drawLoading && scheduleOk && displayScheduleMessage(scheduleOk) && !booking && (
        <div style={{ fontSize: 11, color: '#6b7280', marginTop: 2 }}>{displayScheduleMessage(scheduleOk)}</div>
      )}

      {/* Single primary action */}
      <div style={{ marginTop: 'auto', paddingTop: 10 }}>
        {booking ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {onConfirm && (
              <button onClick={onConfirm} disabled={busy} style={confirmBtnStyle}>
                {busy ? t('bookings.action.confirming') : t('bookings.action.confirmUsage')}
              </button>
            )}
            {onCancel && (
              <button onClick={onCancel} disabled={busy} style={cancelBtnStyle}>
                {busy ? t('bookings.action.cancelling') : t('bookings.action.cancel')}
              </button>
            )}
            {!onCancel && !onConfirm && onDetails && (
              <button onClick={onDetails} className="btn-link" style={{ width: '100%' }}>{t('bookings.action.viewDetails')}</button>
            )}
          </div>
        ) : !drawLoading && scheduleOk?.canRequest ? (
          <button onClick={onRequest} style={requestBtnStyle}>{t('bookings.action.requestSpot')}</button>
        ) : !drawLoading && !scheduleOk ? null : !drawLoading && !scheduleOk?.canRequest ? (
          <div style={{ fontSize: 11, color: '#9ca3af' }}>
            {(scheduleOk && displayCannotRequestReason(scheduleOk)) || t('bookings.tile.requestsNotOpen')}
          </div>
        ) : null}
      </div>
    </div>
  );
}

const tileStyle: React.CSSProperties = {
  background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8,
  padding: '14px 16px', display: 'flex', flexDirection: 'column', minHeight: 130,
};
const tileDayStyle: React.CSSProperties = { fontSize: 12, fontWeight: 700, color: '#374151', textTransform: 'uppercase', letterSpacing: 0.5 };
// UXPOL001 (#798): tile actions share the 38px minimum target of the .btn-* classes.
const requestBtnStyle: React.CSSProperties = { background: 'var(--brand-primary)', color: '#fff', border: 'none', borderRadius: 6, minHeight: 38, padding: '8px 10px', fontSize: 13, fontWeight: 600, cursor: 'pointer', width: '100%' };
const confirmBtnStyle: React.CSSProperties = { background: 'var(--success)', color: '#fff', border: 'none', borderRadius: 6, minHeight: 38, padding: '8px 10px', fontSize: 13, fontWeight: 600, cursor: 'pointer', width: '100%' };
const cancelBtnStyle: React.CSSProperties = { background: '#fff', border: '1px solid var(--danger)', color: 'var(--danger)', borderRadius: 6, minHeight: 38, padding: '8px 10px', fontSize: 13, fontWeight: 600, cursor: 'pointer', width: '100%' };
