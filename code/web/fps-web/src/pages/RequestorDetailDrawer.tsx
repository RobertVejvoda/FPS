import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchHrRequestorSummary,
  updateRequestorEligibility,
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
import { t, tDynamic, tPlural } from '../i18n';

// Displayed label for a booking status code. The underlying value stays the
// English code wherever it drives logic.
function statusLabel(status: string): string {
  return tDynamic('hr.status', status, status);
}

function yesNo(value: boolean): string {
  return value ? t('hr.drawer.yes') : t('hr.drawer.no');
}

// Profile status is a free-text field from the profile service; only the
// well-known 'Active' value has a translation, everything else falls back
// to the raw value (matches the tDynamic fallback pattern used elsewhere).
function profileStatusLabel(status: string): string {
  return tDynamic('hr.drawer.profileStatusValue', status, status);
}

// Vehicle type comes from the HR vehicle import (`car` | `motorcycle` | `van`,
// see HrImportPage). Those machine values stay verbatim in the CSV contract;
// here we only translate how they're displayed to HR.
function vehicleTypeLabel(vehicleType: string): string {
  return tDynamic('hr.drawer.vehicleTypeValue', vehicleType, vehicleType);
}

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
      else if (result.kind === 'unauthenticated') setState({ kind: 'error', message: t('hr.drawer.sessionExpired') });
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
    ? (state.summary.displayName ?? t('labels.requestor.withRef', { ref: state.summary.shortRef }))
    : (displayName ?? displayRequestorRef(request.requestorRef));
  const headerShortRef = state.kind === 'ok' ? state.summary.shortRef : null;

  return (
    <div role="dialog" aria-modal="true" aria-label={t('hr.drawer.ariaLabel')}
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
          <button onClick={onClose} aria-label={t('hr.drawer.closeAria')}
            style={{ background: 'transparent', border: 'none', cursor: 'pointer', fontSize: '1.1rem',
              padding: '0.25rem 0.5rem', borderRadius: 4, color: '#64748b', flexShrink: 0 }}>
            ✕
          </button>
        </header>

        <div style={{ flex: 1, overflow: 'auto', padding: '1rem 1.25rem', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          {state.kind === 'loading' && (
            <p style={{ color: '#6b7280', fontSize: '0.875rem' }}>{t('hr.drawer.loading')}</p>
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
              {t('hr.drawer.notFound')}
              {state.shortRef && (
                <div style={{ marginTop: 4, fontSize: '0.75rem', fontFamily: 'monospace', color: '#78350f' }}>
                  {t('hr.drawer.supportRef', { ref: state.shortRef })}
                </div>
              )}
            </div>
          )}

          {state.kind === 'ok' && (
            <>
              <ProfileFactsSection
                summary={state.summary}
                userId={request.requestorRef}
                onEligibilityChanged={(hasCompanyCar, accessibilityEligible) =>
                  setState(prev => prev.kind === 'ok'
                    ? { kind: 'ok', summary: { ...prev.summary, hasCompanyCar, accessibilityEligible } }
                    : prev)}
              />
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

// HR-controlled eligibility row. Click "Edit" to toggle company car or
// accessibility eligibility for this requestor. Only HR/admin can call
// the backend, so the controls are silently disabled for any role that
// hits a 403 (we treat that as "not your switch to flip"). Issue #481.
function ProfileFactsSection({
  summary, userId, onEligibilityChanged,
}: {
  summary: RequestorSummary;
  userId: string;
  onEligibilityChanged: (hasCompanyCar: boolean, accessibilityEligible: boolean) => void;
}) {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [editing, setEditing] = useState(false);
  const [draftCompanyCar, setDraftCompanyCar] = useState(summary.hasCompanyCar);
  const [draftAccessibility, setDraftAccessibility] = useState(summary.accessibilityEligible);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function startEdit() {
    setDraftCompanyCar(summary.hasCompanyCar);
    setDraftAccessibility(summary.accessibilityEligible);
    setError(null);
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setError(null);
  }

  async function save() {
    setBusy(true);
    setError(null);
    const result = await updateRequestorEligibility({ apiBaseUrl, bearerToken }, userId, {
      hasCompanyCar: draftCompanyCar,
      accessibilityEligible: draftAccessibility,
    });
    setBusy(false);
    if (result.kind === 'ok') {
      onEligibilityChanged(result.data.hasCompanyCar, result.data.accessibilityEligible);
      setEditing(false);
      return;
    }
    if (result.kind === 'error' && result.status === 403) {
      setError(t('hr.drawer.editForbidden'));
      return;
    }
    setError('message' in result ? result.message : t('hr.drawer.saveError'));
  }

  const unchanged = draftCompanyCar === summary.hasCompanyCar
    && draftAccessibility === summary.accessibilityEligible;

  return (
    <section>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 }}>
        <SectionHeading>{t('hr.drawer.parkingFactsTitle')}</SectionHeading>
        {!editing && (
          <button onClick={startEdit} style={editLinkBtn} type="button">{t('hr.drawer.edit')}</button>
        )}
      </div>
      <FactRow label={t('hr.drawer.profileStatus')} value={profileStatusLabel(summary.profileStatus)} muted={summary.profileStatus !== 'Active'} />
      <FactRow label={t('hr.drawer.parkingEligible')} value={yesNo(summary.parkingEligible)} />
      {editing ? (
        <>
          <EditableToggleRow
            label={t('hr.drawer.companyCar')}
            value={draftCompanyCar}
            onChange={setDraftCompanyCar}
            disabled={busy}
          />
          <EditableToggleRow
            label={t('hr.drawer.accessibilityEligible')}
            value={draftAccessibility}
            onChange={setDraftAccessibility}
            disabled={busy}
          />
        </>
      ) : (
        <>
          <FactRow label={t('hr.drawer.companyCar')} value={yesNo(summary.hasCompanyCar)} />
          <FactRow label={t('hr.drawer.accessibilityEligible')} value={yesNo(summary.accessibilityEligible)} />
        </>
      )}
      <FactRow label={t('hr.drawer.reservedSpace')} value={yesNo(summary.reservedSpaceEligible)} />
      {editing && (
        <div style={{ marginTop: 10, display: 'flex', gap: 8, alignItems: 'center' }}>
          <button
            onClick={save}
            disabled={busy || unchanged}
            type="button"
            style={{ ...primaryBtn, opacity: busy || unchanged ? 0.5 : 1 }}
          >
            {busy ? t('hr.drawer.saving') : t('hr.drawer.save')}
          </button>
          <button onClick={cancelEdit} disabled={busy} type="button" style={ghostBtn}>
            {t('hr.drawer.cancel')}
          </button>
          {error && <span style={{ color: '#b91c1c', fontSize: '0.78rem' }}>{error}</span>}
        </div>
      )}
    </section>
  );
}

function EditableToggleRow({
  label, value, onChange, disabled,
}: {
  label: string;
  value: boolean;
  onChange: (next: boolean) => void;
  disabled: boolean;
}) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center',
      padding: '0.3rem 0', borderBottom: '1px solid #f1f5f9', gap: '0.75rem' }}>
      <span style={{ fontSize: '0.78rem', color: '#64748b' }}>{label}</span>
      <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, cursor: disabled ? 'default' : 'pointer' }}>
        <input
          type="checkbox"
          checked={value}
          disabled={disabled}
          onChange={e => onChange(e.target.checked)}
        />
        <span style={{ fontSize: '0.85rem', fontWeight: 600, color: '#0f172a' }}>
          {yesNo(value)}
        </span>
      </label>
    </div>
  );
}

function VehicleSection({ summary }: { summary: RequestorSummary }) {
  return (
    <section>
      <SectionHeading>{t('hr.drawer.vehicleTitle')}</SectionHeading>
      {summary.activeVehicleCount === 0 && (
        <p style={{ fontSize: '0.85rem', color: '#6b7280', margin: 0 }}>{t('hr.drawer.noVehicle')}</p>
      )}
      {summary.defaultVehicle && (
        <>
          <FactRow
            label={t('hr.drawer.defaultPlate')}
            value={summary.defaultVehicle.licensePlate}
            badge={summary.defaultVehicle.isDefault ? t('hr.drawer.badgeDefault') : t('hr.drawer.badgeActive')}
          />
          <FactRow label={t('hr.drawer.vehicleType')} value={vehicleTypeLabel(summary.defaultVehicle.vehicleType)} />
          <FactRow label={t('hr.drawer.electric')} value={yesNo(summary.defaultVehicle.isElectric)} />
          {summary.activeVehicleCount > 1 && (
            <p style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: 4 }}>
              {tPlural('hr.drawer.moreVehicles', summary.activeVehicleCount - 1)}
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
      <SectionHeading>{t('hr.drawer.requestTitle')}</SectionHeading>
      <FactRow label={t('hr.drawer.status')} value={statusLabel(request.status)} />
      <FactRow label={t('hr.drawer.date')} value={displayDate(request.requestedDate)} />
      <FactRow label={t('hr.drawer.location')} value={displayLocation(request.locationId) ?? request.locationId ?? '—'} />
      {timeWindow && <FactRow label={t('hr.drawer.time')} value={timeWindow} />}
      {request.allocatedSlotId && (
        <FactRow label={t('hr.drawer.space')} value={displaySlot(request.allocatedSlotId) ?? request.allocatedSlotId} />
      )}
      <FactRow label={t('hr.drawer.lastUpdated')} value={displayDateTime(request.lastStatusChangedAt)} muted />
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
      <SectionHeading>{t('hr.drawer.historyTitle')}</SectionHeading>
      <Link
        to={`/hr-operations/employees/${encodeURIComponent(userId)}/history`}
        onClick={onNavigate}
        style={{ display: 'inline-flex', alignItems: 'center', gap: 6,
          padding: '0.45rem 0.75rem', borderRadius: 6, background: '#eff6ff',
          color: 'var(--brand-primary)', textDecoration: 'none', fontSize: '0.85rem',
          fontWeight: 600, border: '1px solid #bfdbfe' }}
      >
        {t('hr.drawer.viewHistory')}
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

const editLinkBtn: React.CSSProperties = {
  background: 'transparent',
  border: 'none',
  padding: 0,
  fontSize: '0.75rem',
  fontWeight: 600,
  color: 'var(--brand-primary)',
  cursor: 'pointer',
  textDecoration: 'underline',
};

const primaryBtn: React.CSSProperties = {
  background: 'var(--brand-primary)',
  color: '#fff',
  border: 'none',
  borderRadius: 6,
  padding: '5px 12px',
  fontSize: 13,
  fontWeight: 600,
  cursor: 'pointer',
};

const ghostBtn: React.CSSProperties = {
  background: '#fff',
  color: '#374151',
  border: '1px solid #e5e7eb',
  borderRadius: 6,
  padding: '5px 12px',
  fontSize: 13,
  fontWeight: 500,
  cursor: 'pointer',
};

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
