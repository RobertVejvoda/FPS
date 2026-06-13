import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchHrRequestorSummary,
  type RequestorSummary,
  type RequestorSummaryResult,
} from '../api/profile';
import type { HrBookingListItem } from '../api/bookings';
import {
  displayDate,
  displayDateTime,
  displayLocation,
  displayRequestorRef,
  displaySlot,
  humanizeHrRejection,
} from '../displayLabels';

interface Props {
  request: HrBookingListItem;
  displayName: string | null;
  onClose: () => void;
}

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ok'; summary: RequestorSummary }
  | { kind: 'not-found'; shortRef: string }
  | { kind: 'error'; message: string };

export function RequestorDetailDrawer({ request, displayName, onClose }: Props) {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [state, setState] = useState<LoadState>({ kind: 'loading' });

  useEffect(() => {
    let cancelled = false;
    setState({ kind: 'loading' });
    void fetchHrRequestorSummary({ apiBaseUrl, bearerToken }, request.requestorRef).then((result: RequestorSummaryResult) => {
      if (cancelled) return;
      if (result.kind === 'ok') setState({ kind: 'ok', summary: result.data });
      else if (result.kind === 'not-found') setState({ kind: 'not-found', shortRef: result.shortRef });
      else if (result.kind === 'unauthenticated') setState({ kind: 'error', message: 'Session expired. Please sign in again.' });
      else if (result.kind === 'unreachable') setState({ kind: 'error', message: result.message });
      else setState({ kind: 'error', message: result.message });
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, request.requestorRef]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => { window.removeEventListener('keydown', onKey); };
  }, [onClose]);

  const headerName = state.kind === 'ok'
    ? (state.summary.displayName ?? `Requestor ${state.summary.shortRef}`)
    : (displayName ?? displayRequestorRef(request.requestorRef));
  const headerShortRef = state.kind === 'ok' ? state.summary.shortRef : null;

  return (
    <div role="dialog" aria-modal="true" aria-label="Requestor detail"
         style={{ position: 'fixed', inset: 0, zIndex: 200, display: 'flex' }}>
      {/* Backdrop */}
      <div onClick={onClose} style={{ flex: 1, background: 'rgba(15, 23, 42, 0.4)' }} />
      {/* Panel */}
      <aside style={{
        width: '100%', maxWidth: 420, background: '#fff', boxShadow: '-4px 0 16px rgba(15, 23, 42, 0.08)',
        display: 'flex', flexDirection: 'column', overflow: 'hidden'
      }}>
        <header style={{ padding: '1rem 1.25rem', borderBottom: '1px solid #e5e7eb', display: 'flex',
          alignItems: 'flex-start', justifyContent: 'space-between', gap: '0.75rem' }}>
          <div style={{ minWidth: 0 }}>
            <div style={{ fontSize: '1rem', fontWeight: 700, color: '#0f172a', wordBreak: 'break-word' }}>{headerName}</div>
            {headerShortRef && (
              <div style={{ fontSize: '0.75rem', color: '#64748b', fontFamily: 'monospace', marginTop: 2 }}>
                #{headerShortRef}
              </div>
            )}
          </div>
          <button onClick={onClose} aria-label="Close requestor detail"
            style={{ background: 'transparent', border: 'none', cursor: 'pointer', fontSize: '1.1rem',
              padding: '0.25rem 0.5rem', borderRadius: 4, color: '#64748b', flexShrink: 0 }}>
            ✕
          </button>
        </header>

        <div style={{ flex: 1, overflow: 'auto', padding: '1rem 1.25rem', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          {state.kind === 'loading' && (
            <p style={{ color: '#6b7280', fontSize: '0.875rem' }}>Loading requestor details…</p>
          )}

          {state.kind === 'error' && (
            <div style={{ background: '#fef2f2', border: '1px solid #fecaca', color: '#991b1b',
              borderRadius: 6, padding: '0.625rem 0.75rem', fontSize: '0.85rem' }}>
              {state.message}
            </div>
          )}

          {state.kind === 'not-found' && (
            <div style={{ background: '#fffbeb', border: '1px solid #fcd34d', color: '#92400e',
              borderRadius: 6, padding: '0.625rem 0.75rem', fontSize: '0.85rem' }}>
              Profile details are not available for this requestor.
              {state.shortRef && (
                <div style={{ marginTop: 4, fontSize: '0.75rem', fontFamily: 'monospace', color: '#78350f' }}>
                  Support ref: #{state.shortRef}
                </div>
              )}
            </div>
          )}

          {state.kind === 'ok' && (
            <>
              <ProfileFactsSection summary={state.summary} />
              <VehicleSection summary={state.summary} />
            </>
          )}

          <RequestSection request={request} />

          <HistoryLink userId={request.requestorRef} onNavigate={onClose} />
        </div>
      </aside>
    </div>
  );
}

function ProfileFactsSection({ summary }: { summary: RequestorSummary }) {
  return (
    <section>
      <SectionHeading>Parking facts</SectionHeading>
      <FactRow label="Profile status" value={summary.profileStatus} muted={summary.profileStatus !== 'Active'} />
      <FactRow label="Parking eligible" value={summary.parkingEligible ? 'Yes' : 'No'} />
      <FactRow label="Company car" value={summary.hasCompanyCar ? 'Yes' : 'No'} />
      <FactRow label="Accessibility eligible" value={summary.accessibilityEligible ? 'Yes' : 'No'} />
      <FactRow label="Reserved space" value={summary.reservedSpaceEligible ? 'Yes' : 'No'} />
    </section>
  );
}

function VehicleSection({ summary }: { summary: RequestorSummary }) {
  return (
    <section>
      <SectionHeading>Vehicle</SectionHeading>
      {summary.activeVehicleCount === 0 && (
        <p style={{ fontSize: '0.85rem', color: '#6b7280', margin: 0 }}>No active vehicle on file.</p>
      )}
      {summary.defaultVehicle && (
        <>
          <FactRow
            label="Default plate"
            value={summary.defaultVehicle.licensePlate}
            badge={summary.defaultVehicle.isDefault ? 'Default' : 'Active'}
          />
          <FactRow label="Type" value={summary.defaultVehicle.vehicleType} />
          <FactRow label="Electric" value={summary.defaultVehicle.isElectric ? 'Yes' : 'No'} />
          {summary.activeVehicleCount > 1 && (
            <p style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: 4 }}>
              +{summary.activeVehicleCount - 1} more active vehicle{summary.activeVehicleCount - 1 === 1 ? '' : 's'}.
            </p>
          )}
        </>
      )}
    </section>
  );
}

function RequestSection({ request }: { request: HrBookingListItem }) {
  const timeWindow = request.timeSlotStart && request.timeSlotEnd
    ? `${request.timeSlotStart.slice(0, 5)}–${request.timeSlotEnd.slice(0, 5)}`
    : null;
  const reasonText = (request.reasonCode || request.reason)
    ? humanizeHrRejection(request.reasonCode, request.reason)
    : null;

  return (
    <section>
      <SectionHeading>This request</SectionHeading>
      <FactRow label="Status" value={request.status} />
      <FactRow label="Date" value={displayDate(request.requestedDate)} />
      <FactRow label="Location" value={displayLocation(request.locationId) ?? request.locationId ?? '—'} />
      {timeWindow && <FactRow label="Time" value={timeWindow} />}
      {request.allocatedSlotId && (
        <FactRow label="Space" value={displaySlot(request.allocatedSlotId) ?? request.allocatedSlotId} />
      )}
      <FactRow label="Last updated" value={displayDateTime(request.lastStatusChangedAt)} muted />
      {reasonText && (
        <div style={{ marginTop: '0.625rem', fontSize: '0.8rem', color: '#92400e', background: '#fffbeb',
          border: '1px solid #fcd34d', borderRadius: 4, padding: '0.4rem 0.625rem' }}>
          {reasonText}
        </div>
      )}
    </section>
  );
}

function HistoryLink({ userId, onNavigate }: { userId: string; onNavigate: () => void }) {
  if (!userId) return null;
  return (
    <section>
      <SectionHeading>History</SectionHeading>
      <Link
        to={`/hr-operations/employees/${encodeURIComponent(userId)}/history`}
        onClick={onNavigate}
        style={{ display: 'inline-flex', alignItems: 'center', gap: 6,
          padding: '0.45rem 0.75rem', borderRadius: 6, background: '#eff6ff',
          color: '#1d4ed8', textDecoration: 'none', fontSize: '0.85rem',
          fontWeight: 600, border: '1px solid #bfdbfe' }}
      >
        View parking history →
      </Link>
    </section>
  );
}

function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h3 style={{ fontSize: '0.7rem', fontWeight: 700, color: '#64748b', textTransform: 'uppercase',
      letterSpacing: '0.04em', margin: '0 0 0.5rem 0' }}>
      {children}
    </h3>
  );
}

function FactRow({ label, value, muted, badge }: { label: string; value: string; muted?: boolean; badge?: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: '0.75rem',
      padding: '0.3rem 0', borderBottom: '1px solid #f1f5f9' }}>
      <span style={{ fontSize: '0.78rem', color: '#64748b' }}>{label}</span>
      <span style={{ fontSize: '0.85rem', fontWeight: 600, color: muted ? '#94a3b8' : '#0f172a',
        textAlign: 'right' }}>
        {value}
        {badge && (
          <span style={{ marginLeft: 6, fontSize: '0.65rem', fontWeight: 700, color: '#0369a1',
            background: '#e0f2fe', border: '1px solid #bae6fd', padding: '1px 6px', borderRadius: 10,
            textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            {badge}
          </span>
        )}
      </span>
    </div>
  );
}
