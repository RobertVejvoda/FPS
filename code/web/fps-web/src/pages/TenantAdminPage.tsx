import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchMe } from '../api/client';
import { fetchTenant, fetchTenantReadiness } from '../api/customer';
import type { TenantResponse, ReadinessReportResponse, ReadinessCheckDto } from '../api/customer';

// ── types ─────────────────────────────────────────────────────────────────────

type MeState =
  | { kind: 'loading' }
  | { kind: 'ready'; tenantId: string; isAdmin: boolean }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

type TenantState =
  | { kind: 'loading' }
  | { kind: 'ok'; tenant: TenantResponse }
  | { kind: 'error'; message: string };

type ReadinessState =
  | { kind: 'loading' }
  | { kind: 'ok'; report: ReadinessReportResponse }
  | { kind: 'error'; message: string };

// ── next-action derivation ────────────────────────────────────────────────────

interface NextAction {
  label: string;
  detail: string;
  link?: string;
}

function deriveNextAction(checks: ReadinessCheckDto[]): NextAction | null {
  const failedCheck = checks.find(c => c.status === 'Failed');
  if (!failedCheck) return null;

  const name = failedCheck.name;
  switch (name) {
    case 'LifecycleState':
      return {
        label: 'Advance lifecycle state',
        detail: 'Tenant is in a state that does not allow live use. Transition to Configured or Seeded.',
      };
    case 'IdentityConfig':
      return {
        label: 'Configure identity',
        detail: 'No trusted issuer or audience has been configured for this tenant. Use the Customer API to set up identity.',
      };
    case 'ActiveAdmin':
      return {
        label: 'Add a first administrator',
        detail: 'No active admin is registered. Register an SSO-mapped or local administrator via the Customer API.',
      };
    case 'RoleMapping':
      return {
        label: 'Fix role mapping',
        detail: failedCheck.reason ?? 'Role mapping references unknown FPS roles. Update identity configuration.',
      };
    case 'ParkingPolicy':
      return {
        label: 'Bootstrap parking policy',
        detail: 'No default parking policy has been set. Use the Customer API to record a bootstrap policy.',
        link: '/configuration',
      };
    case 'ParkingLocation':
      return {
        label: 'Add a location with slots',
        detail: 'No location with active slots is configured. Add at least one location via the Customer API.',
        link: '/configuration',
      };
    case 'ProfileFacts':
      return {
        label: 'Load employee/profile facts',
        detail: 'Profile service probe is not connected or reports no pilot user facts. Use the Profile bootstrap API to load required employee data.',
      };
    case 'BookingSmokeTest':
      return {
        label: 'Connect booking service probe',
        detail: 'Booking readiness probe is not connected. Wire the probe before marking this tenant Ready.',
      };
    case 'NotificationReachable':
      return {
        label: 'Connect notification service probe',
        detail: 'Notification readiness probe is not connected. Wire the probe before marking this tenant Ready.',
      };
    case 'AuditEvidence':
      return {
        label: 'Connect audit service probe',
        detail: 'Audit readiness probe is not connected. Wire the probe before marking this tenant Ready.',
      };
    case 'ReportingEvidence':
      return {
        label: 'Connect reporting service probe',
        detail: 'Reporting readiness probe is not connected. Wire the probe before marking this tenant Ready.',
      };
    default:
      return { label: `Resolve ${name}`, detail: failedCheck.reason ?? 'Check failed.' };
  }
}

// ── page ──────────────────────────────────────────────────────────────────────

export function TenantAdminPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const cfg = { apiBaseUrl, bearerToken };

  const [me, setMe] = useState<MeState>({ kind: 'loading' });
  const [tenantState, setTenantState] = useState<TenantState>({ kind: 'loading' });
  const [readinessState, setReadinessState] = useState<ReadinessState>({ kind: 'loading' });

  const load = useCallback(() => {
    setMe({ kind: 'loading' });
    setTenantState({ kind: 'loading' });
    setReadinessState({ kind: 'loading' });

    fetchMe(cfg).then(r => {
      if (r.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (r.kind !== 'ok') {
        setMe({ kind: 'error', message: 'message' in r ? r.message : 'Failed to load identity.' });
        return;
      }
      const { tenantId, roles } = r.data;
      const isAdmin = roles.includes('admin');
      setMe({ kind: 'ready', tenantId, isAdmin });

      if (!isAdmin) {
        setTenantState({ kind: 'error', message: '' });
        setReadinessState({ kind: 'error', message: '' });
        return;
      }

      fetchTenant(cfg, tenantId).then(tr => {
        if (tr.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
        if (tr.kind === 'ok') setTenantState({ kind: 'ok', tenant: tr.data });
        else setTenantState({ kind: 'error', message: 'message' in tr ? tr.message : 'Failed to load tenant.' });
      });

      fetchTenantReadiness(cfg, tenantId).then(rr => {
        if (rr.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
        if (rr.kind === 'ok') setReadinessState({ kind: 'ok', report: rr.data });
        else setReadinessState({ kind: 'error', message: 'message' in rr ? rr.message : 'Failed to load readiness.' });
      });
    });
  }, [apiBaseUrl, bearerToken]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => { load(); }, [load]);

  if (me.kind === 'loading') return <p style={muted}>Loading…</p>;
  if (me.kind === 'error') return (
    <div><p style={{ color: '#b91c1c' }}>{me.message}</p><button onClick={load} style={btn}>Retry</button></div>
  );
  if (me.kind === 'ready' && !me.isAdmin) return (
    <p style={{ color: '#b91c1c' }}>You do not have admin access to view the tenant console.</p>
  );

  const nextAction = readinessState.kind === 'ok'
    ? deriveNextAction(readinessState.kind === 'ok' ? readinessState.report.checks : [])
    : null;

  return (
    <div style={page}>
      <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Tenant Admin</h2>

      {/* Tenant Overview */}
      <section style={card}>
        <h3 style={cardTitle}>Tenant Overview</h3>
        {tenantState.kind === 'loading' && <p style={muted}>Loading…</p>}
        {tenantState.kind === 'error' && tenantState.message && (
          <p style={{ color: '#b91c1c', fontSize: 13 }}>{tenantState.message}</p>
        )}
        {tenantState.kind === 'ok' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <Row label="Name" value={tenantState.tenant.displayName} />
            <Row label="Slug" value={tenantState.tenant.slug} />
            <Row label="Region" value={tenantState.tenant.region} />
            <Row label="Time zone" value={tenantState.tenant.timeZone} />
            <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
              <span style={rowLabel}>Lifecycle</span>
              <LifecycleBadge state={tenantState.tenant.lifecycleState} />
            </div>
            <Row label="Created" value={new Date(tenantState.tenant.createdAt).toLocaleDateString()} />
            <Row label="Updated" value={new Date(tenantState.tenant.updatedAt).toLocaleDateString()} />
          </div>
        )}
      </section>

      {/* Readiness */}
      <section style={card}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <h3 style={{ ...cardTitle, marginBottom: 0 }}>Readiness</h3>
          {readinessState.kind === 'ok' && (
            <ReadinessBadge isReady={readinessState.report.isReady} />
          )}
        </div>

        {readinessState.kind === 'loading' && <p style={muted}>Loading…</p>}
        {readinessState.kind === 'error' && readinessState.message && (
          <p style={{ color: '#b91c1c', fontSize: 13 }}>{readinessState.message}</p>
        )}
        {readinessState.kind === 'ok' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
            {readinessState.report.checks.map(check => (
              <CheckRow key={check.name} check={check} />
            ))}
          </div>
        )}
      </section>

      {/* Next action */}
      {nextAction && (
        <section style={{ ...card, background: '#fffbeb', borderColor: '#fcd34d' }}>
          <h3 style={{ ...cardTitle, color: '#92400e' }}>Next required action</h3>
          <p style={{ margin: '0 0 4px', fontWeight: 600, fontSize: 14 }}>{nextAction.label}</p>
          <p style={{ margin: '0 0 8px', fontSize: 13, color: '#78350f' }}>{nextAction.detail}</p>
          {nextAction.link && (
            <Link to={nextAction.link} style={{ fontSize: 13, color: '#1d4ed8', fontWeight: 500 }}>
              Go to {nextAction.link === '/configuration' ? 'Configuration' : nextAction.link} →
            </Link>
          )}
        </section>
      )}

      {readinessState.kind === 'ok' && readinessState.report.isReady && (
        <section style={{ ...card, background: '#f0fdf4', borderColor: '#86efac' }}>
          <p style={{ margin: 0, fontWeight: 600, color: '#166534', fontSize: 14 }}>
            All checks passed — tenant is ready for live use.
          </p>
        </section>
      )}

      <div style={{ display: 'flex', gap: 8 }}>
        <button onClick={load} style={btnSm}>Refresh</button>
      </div>
    </div>
  );
}

// ── sub-components ────────────────────────────────────────────────────────────

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', gap: 12, alignItems: 'baseline' }}>
      <span style={rowLabel}>{label}</span>
      <span style={{ fontSize: 14 }}>{value}</span>
    </div>
  );
}

function LifecycleBadge({ state }: { state: string }) {
  const colors: Record<string, { bg: string; text: string }> = {
    Draft:      { bg: '#f3f4f6', text: '#374151' },
    Configured: { bg: '#eff6ff', text: '#1d4ed8' },
    Seeded:     { bg: '#f0f9ff', text: '#0369a1' },
    Ready:      { bg: '#f0fdf4', text: '#166534' },
    Suspended:  { bg: '#fef9c3', text: '#854d0e' },
    Archived:   { bg: '#fef2f2', text: '#991b1b' },
  };
  const c = colors[state] ?? { bg: '#f3f4f6', text: '#374151' };
  return (
    <span style={{ background: c.bg, color: c.text, borderRadius: 99, padding: '2px 10px', fontSize: 12, fontWeight: 600 }}>
      {state}
    </span>
  );
}

function ReadinessBadge({ isReady }: { isReady: boolean }) {
  return (
    <span style={{
      background: isReady ? '#f0fdf4' : '#fef2f2',
      color: isReady ? '#166534' : '#991b1b',
      borderRadius: 99, padding: '3px 12px', fontSize: 12, fontWeight: 700,
    }}>
      {isReady ? 'Ready' : 'Not ready'}
    </span>
  );
}

function CheckRow({ check }: { check: ReadinessCheckDto }) {
  const status = check.status;
  const icon = status === 'Passed' ? '✓' : status === 'Failed' ? '✗' : '–';
  const color = status === 'Passed' ? '#166534' : status === 'Failed' ? '#b91c1c' : '#6b7280';
  const bg = status === 'Passed' ? '#f0fdf4' : status === 'Failed' ? '#fef2f2' : '#f9fafb';
  return (
    <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10, padding: '8px 0', borderBottom: '1px solid #f3f4f6' }}>
      <span style={{ color, fontWeight: 700, width: 16, flexShrink: 0, paddingTop: 1 }}>{icon}</span>
      <div style={{ flex: 1 }}>
        <span style={{ fontSize: 13, fontWeight: 600, background: bg, color, borderRadius: 4, padding: '1px 6px' }}>
          {check.name}
        </span>
        {check.reason && (
          <p style={{ margin: '4px 0 0', fontSize: 12, color: '#374151' }}>{check.reason}</p>
        )}
      </div>
    </div>
  );
}

// ── styles ────────────────────────────────────────────────────────────────────

const page: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 16 };
const card: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '16px 20px' };
const cardTitle: React.CSSProperties = { margin: '0 0 12px', fontSize: 15, fontWeight: 700 };
const rowLabel: React.CSSProperties = { fontSize: 12, color: '#6b7280', width: 80, flexShrink: 0 };
const muted: React.CSSProperties = { color: '#6b7280', fontSize: 13 };
const btn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
const btnSm: React.CSSProperties = { ...btn, padding: '6px 12px', fontSize: 13 };
