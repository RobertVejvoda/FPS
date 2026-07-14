import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchHrBookings,
  hrCancelBooking,
  type HrBookingListItem,
} from '../api/bookings';
import { fetchHrDisplayNames } from '../api/profile';
import {
  displayDate,
  displayDateTime,
  displayLocation,
  displayRequestorRef,
  displaySlot,
  humanizeHrRejection,
} from '../displayLabels';
import { NotificationBanner } from '../components/NotificationBanner';
import { nextWorkdayOptions } from '../dateOptions';
import { useTenantDateContext } from '../hooks/useTenantDateBase';
import { RequestorDetailDrawer } from './RequestorDetailDrawer';
import { t, tDynamic, tPlural } from '../i18n';

const LOCATION_ID = 'Prague';
const STATUS_FILTERS = ['All', 'Pending', 'Allocated', 'Cancelled', 'Rejected'];

// Displayed label for a booking status code. The underlying value (used for
// state/filtering/API query params) always stays the English code.
function statusLabel(status: string): string {
  return tDynamic('hr.status', status, status);
}

type ListState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: HrBookingListItem[]; totalCount: number; nextCursor: string | null }
  | { kind: 'error'; message: string };

function statusBadgeStyle(status: string): React.CSSProperties {
  switch (status) {
    case 'Allocated': return { background: '#f0fdf4', color: '#166534', border: '1px solid #bbf7d0' };
    case 'Pending':   return { background: '#fffbeb', color: '#92400e', border: '1px solid #fcd34d' };
    case 'Cancelled': return { background: '#f9fafb', color: '#6b7280', border: '1px solid #e5e7eb' };
    case 'Rejected':  return { background: '#fef2f2', color: '#991b1b', border: '1px solid #fecaca' };
    default:          return { background: '#f9fafb', color: '#6b7280', border: '1px solid #e5e7eb' };
  }
}

interface RequestRowProps {
  item: HrBookingListItem;
  busyId: string | null;
  onCancel: (id: string) => void;
  onOpenDetail: (item: HrBookingListItem) => void;
  displayName?: string | null;
}

function RequestRow({ item, busyId, onCancel, onOpenDetail, displayName }: RequestRowProps) {
  const primaryLabel = displayName ?? displayRequestorRef(item.requestorRef);
  const secondaryRef = displayName ? displayRequestorRef(item.requestorRef) : null;
  const locationLabel = displayLocation(item.locationId) ?? displayLocation(LOCATION_ID) ?? t('hr.ops.locationNotSet');
  const timeWindow = item.timeSlotStart && item.timeSlotEnd
    ? `${item.timeSlotStart.slice(0, 5)}–${item.timeSlotEnd.slice(0, 5)}`
    : null;
  const reasonText = (item.reasonCode || item.reason) ? humanizeHrRejection(item.reasonCode, item.reason) : null;
  const canCancel = item.status === 'Pending' || item.status === 'Allocated';
  const shortId = `#${item.requestId.replace(/-/g, '').slice(-6).toUpperCase()}`;

  return (
    <li style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '0.875rem 1rem' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '0.75rem', flexWrap: 'wrap' }}>
        <div style={{ flex: '1 1 0', minWidth: 0 }}>
          {/* Primary row: name + status */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem', flexWrap: 'wrap', marginBottom: '0.375rem' }}>
            <button
              type="button"
              onClick={() => onOpenDetail(item)}
              title={t('hr.ops.openRequestorDetail')}
              style={{ fontSize: '0.9rem', fontWeight: 700, color: '#1d4ed8', background: 'transparent',
                border: 'none', padding: 0, cursor: 'pointer', textAlign: 'left',
                textDecoration: 'underline', textDecorationStyle: 'dotted', textDecorationColor: '#cbd5e1',
                textUnderlineOffset: 3 }}
            >
              {primaryLabel}
            </button>
            {secondaryRef && <span style={{ fontSize: '0.75rem', color: '#94a3b8', fontFamily: 'monospace' }}>{secondaryRef}</span>}
            <span style={{ fontSize: '0.75rem', fontWeight: 600, padding: '0.15rem 0.6rem', borderRadius: 12, ...statusBadgeStyle(item.status) }}>
              {statusLabel(item.status)}
            </span>
          </div>
          {/* Secondary row: parking day · location · time · slot */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap', fontSize: '0.8rem', color: '#475569' }}>
            <span style={{ fontWeight: 600, color: '#1e293b' }}>{displayDate(item.requestedDate)}</span>
            <span style={{ color: '#cbd5e1' }}>·</span>
            <span>{locationLabel}</span>
            {timeWindow && <><span style={{ color: '#cbd5e1' }}>·</span><span>{timeWindow}</span></>}
            {item.allocatedSlotId && (
              <><span style={{ color: '#cbd5e1' }}>·</span><span style={{ fontWeight: 600, color: '#166534' }}>{t('hr.ops.spaceLabel', { space: displaySlot(item.allocatedSlotId) ?? item.allocatedSlotId })}</span></>
            )}
          </div>
          {/* Reason box */}
          {reasonText && (
            <div style={{ marginTop: '0.375rem', fontSize: '0.8rem', color: '#92400e', background: '#fffbeb', border: '1px solid #fcd34d', borderRadius: 4, padding: '0.25rem 0.6rem', display: 'inline-block' }}>
              {reasonText}
            </div>
          )}
          {/* Tertiary row: updated + request ID */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.625rem', marginTop: '0.375rem', flexWrap: 'wrap' }}>
            <span style={{ fontSize: '0.73rem', color: '#94a3b8' }}>{t('hr.ops.updatedAt', { time: displayDateTime(item.lastStatusChangedAt) })}</span>
            <span style={{ fontSize: '0.7rem', color: '#cbd5e1', fontFamily: 'monospace' }}>{shortId}</span>
          </div>
        </div>
        {/* Cancel action */}
        {canCancel && (
          <button
            disabled={busyId === item.requestId}
            onClick={() => onCancel(item.requestId)}
            style={{ flexShrink: 0, padding: '0.3rem 0.875rem', borderRadius: 4, border: '1px solid #fca5a5',
              background: '#fff', color: '#dc2626', fontSize: '0.8rem', cursor: 'pointer', alignSelf: 'flex-start' }}
          >
            {t('hr.ops.cancel')}
          </button>
        )}
      </div>
    </li>
  );
}

export function HrOperationsPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();

  const [selectedChip, setSelectedChip] = useState(0);
  const [statusFilter, setStatusFilter] = useState('All');
  const [listState, setListState] = useState<ListState>({ kind: 'loading' });

  const [busyId, setBusyId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelTarget, setCancelTarget] = useState<string | null>(null);
  const [displayNames, setDisplayNames] = useState<Record<string, string | null>>({});
  const [detailRequest, setDetailRequest] = useState<HrBookingListItem | null>(null);

  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);

  const { dateBase, simulationActive } = useTenantDateContext();
  const dateChips = useMemo(() => nextWorkdayOptions(dateBase, 4, { relativeLabels: !simulationActive }), [dateBase, simulationActive]);

  const selectedDate = dateChips[selectedChip]?.date ?? dateChips[0].date;

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

  const loadBookings = useCallback(() => {
    setListState({ kind: 'loading' });
    setDisplayNames({});
    const filter = statusFilter === 'All' ? undefined : statusFilter;
    fetchHrBookings({ apiBaseUrl, bearerToken }, { locationId: LOCATION_ID, from: selectedDate, to: selectedDate, status: filter }).then((result) => {
      if (result.kind === 'unauthenticated' || result.kind === 'forbidden') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setListState({ kind: 'ok', items: result.items, totalCount: result.totalCount, nextCursor: result.nextCursor });
        const uniqueRefs = [...new Set(result.items.map(i => i.requestorRef).filter(Boolean))];
        if (uniqueRefs.length > 0) {
          void fetchHrDisplayNames({ apiBaseUrl, bearerToken }, uniqueRefs).then(r => {
            if (r.kind === 'ok') setDisplayNames(r.data.names);
          });
        }
      } else {
        setListState({ kind: 'error', message: 'message' in result ? result.message : t('hr.ops.loadError') });
      }
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, selectedDate, statusFilter]);

  useEffect(() => { loadBookings(); }, [loadBookings]);

  async function handleHrCancel() {
    if (!cancelTarget || !cancelReason.trim()) return;
    setBusyId(cancelTarget);
    const result = await hrCancelBooking({ apiBaseUrl, bearerToken }, cancelTarget, cancelReason.trim());
    setBusyId(null);
    setCancelTarget(null);
    setCancelReason('');
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') { showToast(true, t('hr.ops.toast.cancelSuccess')); loadBookings(); }
    else showToast(false, 'message' in result ? result.message : t('hr.ops.toast.cancelFailed'));
  }

  return (
    <div className="page-stack">
      <div className="page-hero">
        <div>
          <h2>{t('hr.ops.title')}</h2>
          <p>{displayLocation(LOCATION_ID) ?? LOCATION_ID}</p>
        </div>
      </div>

      <div className="panel">
        <NotificationBanner style={{ marginBottom: '1rem' }} />

        {toast && (
          <div style={{ marginBottom: '1rem', padding: '0.75rem 1rem', borderRadius: 6,
            background: toast.ok ? '#f0fdf4' : '#fef2f2',
            border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`,
            color: toast.ok ? '#166534' : '#991b1b', fontSize: '0.875rem' }}>
            {toast.text}
          </div>
        )}

        {/* Date filter */}
        <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
          {dateChips.map((chip, i) => (
            <button
              key={chip.date}
              onClick={() => setSelectedChip(i)}
              style={{ padding: '0.375rem 0.875rem', borderRadius: 20, border: 'none', cursor: 'pointer', fontSize: '0.875rem',
                background: selectedChip === i ? '#2563eb' : '#f3f4f6',
                color: selectedChip === i ? '#fff' : '#374151', fontWeight: selectedChip === i ? 600 : 400 }}
            >
              {chip.label}
            </button>
          ))}
        </div>

        {/* Status filter */}
        <div style={{ display: 'flex', gap: '0.375rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
          {STATUS_FILTERS.map(s => (
            <button
              key={s}
              onClick={() => setStatusFilter(s)}
              style={{ padding: '0.25rem 0.75rem', borderRadius: 12, border: `1px solid ${statusFilter === s ? '#2563eb' : '#d1d5db'}`,
                background: statusFilter === s ? '#eff6ff' : '#fff', color: statusFilter === s ? '#2563eb' : '#374151',
                fontSize: '0.8rem', cursor: 'pointer' }}
            >
              {statusLabel(s)}
            </button>
          ))}
        </div>

        {/* Request list */}
        {listState.kind === 'loading' && <p style={{ color: '#6b7280', fontSize: '0.875rem' }}>{t('hr.ops.loading')}</p>}
        {listState.kind === 'error' && (
          <div>
            <p style={{ color: '#ef4444', fontSize: '0.875rem' }}>{listState.message}</p>
            <button onClick={loadBookings} className="btn-primary">{t('hr.ops.retry')}</button>
          </div>
        )}
        {listState.kind === 'ok' && (
          <>
            <p style={{ fontSize: '0.8rem', color: '#6b7280', marginBottom: '0.5rem' }}>
              {tPlural('hr.ops.requestCount', listState.totalCount, { date: displayDate(selectedDate) })}
            </p>
            {listState.items.length === 0 && (
              <div style={{ background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 6, padding: '1.5rem', textAlign: 'center' }}>
                <p style={{ color: '#6b7280', fontSize: '0.875rem', margin: 0 }}>
                  {statusFilter !== 'All'
                    ? t('hr.ops.emptyFiltered', { status: statusLabel(statusFilter), date: displayDate(selectedDate) })
                    : t('hr.ops.emptyAll', { date: displayDate(selectedDate) })
                  }
                </p>
              </div>
            )}
            <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              {listState.items.map(item => (
                <RequestRow
                  key={item.requestId}
                  item={item}
                  busyId={busyId}
                  onCancel={setCancelTarget}
                  onOpenDetail={setDetailRequest}
                  displayName={displayNames[item.requestorRef] ?? null}
                />
              ))}
            </ul>
          </>
        )}
      </div>

      {/* Requestor detail drawer */}
      {detailRequest && (
        <RequestorDetailDrawer
          request={detailRequest}
          displayName={displayNames[detailRequest.requestorRef] ?? null}
          onClose={() => setDetailRequest(null)}
        />
      )}

      {/* Cancel modal */}
      {cancelTarget && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: '#fff', borderRadius: 8, padding: '1.5rem', width: '100%', maxWidth: 420, margin: '0 1rem' }}>
            <h2 style={{ fontSize: '1rem', fontWeight: 700, marginBottom: '0.75rem' }}>{t('hr.ops.cancelModal.title')}</h2>
            <p style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '0.75rem' }}>
              {t('hr.ops.cancelModal.body')}
            </p>
            <textarea
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              placeholder={t('hr.ops.cancelModal.reasonPlaceholder')}
              rows={3}
              style={{ width: '100%', padding: '0.5rem', borderRadius: 4, border: '1px solid #d1d5db', fontSize: '0.875rem', boxSizing: 'border-box', resize: 'vertical' }}
            />
            <div style={{ display: 'flex', gap: '0.5rem', marginTop: '1rem', justifyContent: 'flex-end' }}>
              <button
                onClick={() => { setCancelTarget(null); setCancelReason(''); }}
                style={{ padding: '0.4rem 1rem', borderRadius: 4, border: '1px solid #d1d5db', background: '#fff', cursor: 'pointer', fontSize: '0.875rem' }}
              >
                {t('hr.ops.cancelModal.back')}
              </button>
              <button
                disabled={!cancelReason.trim() || busyId === cancelTarget}
                onClick={() => { void handleHrCancel(); }}
                style={{ padding: '0.4rem 1rem', borderRadius: 4, border: 'none', background: '#dc2626', color: '#fff',
                  cursor: !cancelReason.trim() ? 'not-allowed' : 'pointer', fontSize: '0.875rem',
                  opacity: !cancelReason.trim() ? 0.5 : 1 }}
              >
                {busyId === cancelTarget ? t('hr.ops.cancelModal.cancelling') : t('hr.ops.cancelModal.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
