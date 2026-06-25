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

// ── check metadata ────────────────────────────────────────────────────────────

interface CheckMeta {
  label: string;
  purpose: string;
  nextAction: string;
  link?: string;
  linkLabel?: string;
}

const CHECK_META: Record<string, CheckMeta> = {
  LifecycleState: {
    label: 'Tenant lifecycle',
    purpose: 'Tenant must be in an active lifecycle state before employees can book.',
    nextAction: 'Contact your FairSpot operator to advance the tenant lifecycle to Configured or Ready.',
  },
  IdentityConfig: {
    label: 'Identity & login',
    purpose: 'SSO login must be configured so employees can sign in.',
    nextAction: 'Configure your identity provider in the tenant settings, or contact your operator.',
  },
  ActiveAdmin: {
    label: 'Administrator account',
    purpose: 'At least one active admin must exist to manage the tenant.',
    nextAction: 'Add an administrator account, or contact your operator to register the first admin.',
  },
  RoleMapping: {
    label: 'Role configuration',
    purpose: 'Roles must map to valid FairSpot roles (employee, admin, hr_manager, report_viewer).',
    nextAction: 'Fix the role mapping in your identity configuration, or contact your operator.',
  },
  ParkingPolicy: {
    label: 'Parking policy',
    purpose: 'A default parking policy defines the draw schedule and booking rules for your employees.',
    nextAction: 'Set up a default parking policy in Configuration.',
    link: '/configuration',
    linkLabel: 'Configuration',
  },
  ParkingLocation: {
    label: 'Parking locations & capacity',
    purpose: 'At least one location with active parking slots is required for draws to run.',
    nextAction: 'Add a location with at least one active slot in Configuration.',
    link: '/configuration',
    linkLabel: 'Configuration',
  },
  ProfileFacts: {
    label: 'Employee data',
    purpose: 'Employee profiles must be loaded so staff can participate in draws.',
    nextAction: 'Import your employee list in HR Import.',
    link: '/hr-import',
    linkLabel: 'HR Import',
  },
  BookingSmokeTest: {
    label: 'Booking service',
    purpose: 'The booking service must be available to run draws and accept spot requests.',
    nextAction: 'Check that the booking service is running. If the issue persists, contact your operator.',
  },
  NotificationReachable: {
    label: 'Notifications',
    purpose: 'The notification service must be available to inform employees of draw outcomes.',
    nextAction: 'Check that the notification service is running. If the issue persists, contact your operator.',
  },
  AuditEvidence: {
    label: 'Audit trail',
    purpose: 'The audit service must be available to record draw evidence and fairness logs.',
    nextAction: 'Check that the audit service is running. If the issue persists, contact your operator.',
  },
  ReportingEvidence: {
    label: 'Reporting',
    purpose: 'The reporting service must be available for management and compliance reports.',
    nextAction: 'Check that the reporting service is running. If the issue persists, contact your operator.',
  },
  ObjectStorageReadiness: {
    label: 'Document & file storage',
    purpose: 'Tenant file storage enables document uploads, report exports, audit evidence, and branding assets.',
    nextAction: 'Deferred for pilot — file storage provisioning (OPS008C) is not yet implemented. FairSpot will operate without document uploads or branding assets during the pilot.',
  },
  BrandingReadiness: {
    label: 'Organization branding',
    purpose: 'Tenant branding lets your employees see your organization name and logo in FairSpot.',
    nextAction: 'Deferred for pilot — branding configuration (CUST010) is not yet implemented. FairSpot defaults will be shown to employees during the pilot.',
  },
};

function getCheckMeta(name: string): CheckMeta {
  return CHECK_META[name] ?? {
    label: name,
    purpose: '',
    nextAction: 'Investigate and resolve the failing check.',
  };
}

// ── next-action derivation ────────────────────────────────────────────────────

interface NextAction {
  label: string;
  detail: string;
  link?: string;
  linkLabel?: string;
}

function deriveNextAction(checks: ReadinessCheckDto[]): NextAction | null {
  // Only surface Failed checks as the primary next action — Deferred items are known
  // pilot limitations and do not block day-to-day tenant operation.
  const failedCheck = checks.find(c => c.status === 'Failed');
  if (!failedCheck) return null;

  const meta = getCheckMeta(failedCheck.name);
  return {
    label: meta.label,
    detail: failedCheck.reason ? `${meta.nextAction} (${failedCheck.reason})` : meta.nextAction,
    link: meta.link,
    linkLabel: meta.linkLabel,
  };
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
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 }}>
          <h3 style={{ ...cardTitle, marginBottom: 0 }}>Readiness</h3>
          {readinessState.kind === 'ok' && (
            <ReadinessBadge isReady={readinessState.report.isReady} />
          )}
        </div>
        <p style={{ margin: '0 0 12px', fontSize: 12, color: '#6b7280' }}>
          Ready means the tenant is fully configured for employees to request spots, participate in draws, and receive notifications.
        </p>

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
          <h3 style={{ ...cardTitle, color: '#92400e' }}>Action required: {nextAction.label}</h3>
          <p style={{ margin: '0 0 8px', fontSize: 13, color: '#78350f' }}>{nextAction.detail}</p>
          {nextAction.link && (
            <Link to={nextAction.link} style={{ fontSize: 13, color: '#1d4ed8', fontWeight: 500 }}>
              Go to {nextAction.linkLabel ?? nextAction.link} →
            </Link>
          )}
        </section>
      )}

      {readinessState.kind === 'ok' && readinessState.report.isReady && (() => {
        const deferred = readinessState.report.checks.filter(c => c.status === 'Deferred');
        return (
          <section style={{ ...card, background: '#f0fdf4', borderColor: '#86efac' }}>
            <p style={{ margin: 0, fontWeight: 600, color: '#166534', fontSize: 14 }}>
              {deferred.length > 0
                ? 'Tenant is pilot-ready — required checks passed.'
                : 'All checks passed — tenant is ready for live use.'}
            </p>
            {deferred.length > 0 && (
              <p style={{ margin: '6px 0 0', fontSize: 12, color: '#166534' }}>
                {deferred.length} item{deferred.length > 1 ? 's are' : ' is'} deferred for the pilot and will not block day-to-day operation.
                Resolve before moving to production: {deferred.map(c => getCheckMeta(c.name).label).join(', ')}.
              </p>
            )}
          </section>
        );
      })()}

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
  const icon =
    status === 'Passed' ? '✓' :
    status === 'Failed' ? '✗' :
    status === 'Deferred' ? '~' : '–';
  const color =
    status === 'Passed' ? '#166534' :
    status === 'Failed' ? '#b91c1c' :
    status === 'Deferred' ? '#92400e' : '#6b7280';
  const bg =
    status === 'Passed' ? '#f0fdf4' :
    status === 'Failed' ? '#fef2f2' :
    status === 'Deferred' ? '#fffbeb' : '#f9fafb';
  const meta = getCheckMeta(check.name);
  const showNextAction = status !== 'Passed';
  const nextActionColor =
    status === 'Failed' ? '#991b1b' :
    status === 'Deferred' ? '#78350f' : '#6b7280';
  return (
    <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10, padding: '10px 0', borderBottom: '1px solid #f3f4f6' }}>
      <span style={{ color, fontWeight: 700, width: 16, flexShrink: 0, paddingTop: 2 }}>{icon}</span>
      <div style={{ flex: 1 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 13, fontWeight: 600, background: bg, color, borderRadius: 4, padding: '1px 6px' }}>
            {meta.label}
            {status === 'Deferred' && <span style={{ fontWeight: 400, marginLeft: 4 }}>(pilot deferred)</span>}
          </span>
          {meta.purpose && (
            <span style={{ fontSize: 12, color: '#6b7280' }}>{meta.purpose}</span>
          )}
        </div>
        {showNextAction && (
          <p style={{ margin: '4px 0 0', fontSize: 12, color: nextActionColor }}>
            {meta.nextAction}
            {meta.link && (
              <> — <Link to={meta.link} style={{ color: '#1d4ed8', fontWeight: 500 }}>Go to {meta.linkLabel ?? meta.link} →</Link></>
            )}
          </p>
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
