import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchMe } from '../api/client';
import { fetchTenant, fetchTenantReadiness } from '../api/customer';
import type { TenantResponse, ReadinessReportResponse, ReadinessCheckDto } from '../api/customer';
import { TenantIdentitySettingsSection } from './TenantIdentitySettingsSection';
import { t, tDynamic, tPlural, formatDate, type MessageKey } from '../i18n';

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

interface CheckMetaKeys {
  labelKey: MessageKey;
  purposeKey: MessageKey;
  nextActionKey: MessageKey;
  link?: string;
  linkLabelKey?: MessageKey;
}

const CHECK_META: Record<string, CheckMetaKeys> = {
  LifecycleState: {
    labelKey: 'admin.readiness.check.LifecycleState.label',
    purposeKey: 'admin.readiness.check.LifecycleState.purpose',
    nextActionKey: 'admin.readiness.check.LifecycleState.nextAction',
  },
  IdentityConfig: {
    labelKey: 'admin.readiness.check.IdentityConfig.label',
    purposeKey: 'admin.readiness.check.IdentityConfig.purpose',
    nextActionKey: 'admin.readiness.check.IdentityConfig.nextAction',
  },
  ActiveAdmin: {
    labelKey: 'admin.readiness.check.ActiveAdmin.label',
    purposeKey: 'admin.readiness.check.ActiveAdmin.purpose',
    nextActionKey: 'admin.readiness.check.ActiveAdmin.nextAction',
  },
  RoleMapping: {
    labelKey: 'admin.readiness.check.RoleMapping.label',
    purposeKey: 'admin.readiness.check.RoleMapping.purpose',
    nextActionKey: 'admin.readiness.check.RoleMapping.nextAction',
  },
  ParkingPolicy: {
    labelKey: 'admin.readiness.check.ParkingPolicy.label',
    purposeKey: 'admin.readiness.check.ParkingPolicy.purpose',
    nextActionKey: 'admin.readiness.check.ParkingPolicy.nextAction',
    link: '/configuration',
    linkLabelKey: 'nav.configuration',
  },
  ParkingLocation: {
    labelKey: 'admin.readiness.check.ParkingLocation.label',
    purposeKey: 'admin.readiness.check.ParkingLocation.purpose',
    nextActionKey: 'admin.readiness.check.ParkingLocation.nextAction',
    link: '/configuration',
    linkLabelKey: 'nav.configuration',
  },
  ProfileFacts: {
    labelKey: 'admin.readiness.check.ProfileFacts.label',
    purposeKey: 'admin.readiness.check.ProfileFacts.purpose',
    nextActionKey: 'admin.readiness.check.ProfileFacts.nextAction',
    link: '/hr-import',
    linkLabelKey: 'nav.hrImport',
  },
  BookingSmokeTest: {
    labelKey: 'admin.readiness.check.BookingSmokeTest.label',
    purposeKey: 'admin.readiness.check.BookingSmokeTest.purpose',
    nextActionKey: 'admin.readiness.check.BookingSmokeTest.nextAction',
  },
  NotificationReachable: {
    labelKey: 'admin.readiness.check.NotificationReachable.label',
    purposeKey: 'admin.readiness.check.NotificationReachable.purpose',
    nextActionKey: 'admin.readiness.check.NotificationReachable.nextAction',
  },
  AuditEvidence: {
    labelKey: 'admin.readiness.check.AuditEvidence.label',
    purposeKey: 'admin.readiness.check.AuditEvidence.purpose',
    nextActionKey: 'admin.readiness.check.AuditEvidence.nextAction',
  },
  ReportingEvidence: {
    labelKey: 'admin.readiness.check.ReportingEvidence.label',
    purposeKey: 'admin.readiness.check.ReportingEvidence.purpose',
    nextActionKey: 'admin.readiness.check.ReportingEvidence.nextAction',
  },
  ObjectStorageReadiness: {
    labelKey: 'admin.readiness.check.ObjectStorageReadiness.label',
    purposeKey: 'admin.readiness.check.ObjectStorageReadiness.purpose',
    nextActionKey: 'admin.readiness.check.ObjectStorageReadiness.nextAction',
  },
  BrandingReadiness: {
    labelKey: 'admin.readiness.check.BrandingReadiness.label',
    purposeKey: 'admin.readiness.check.BrandingReadiness.purpose',
    nextActionKey: 'admin.readiness.check.BrandingReadiness.nextAction',
  },
};

function getCheckMeta(name: string): CheckMeta {
  const meta = CHECK_META[name];
  if (!meta) {
    return { label: name, purpose: '', nextAction: t('admin.readiness.fallbackNextAction') };
  }
  return {
    label: t(meta.labelKey),
    purpose: t(meta.purposeKey),
    nextAction: t(meta.nextActionKey),
    link: meta.link,
    linkLabel: meta.linkLabelKey ? t(meta.linkLabelKey) : undefined,
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
        setMe({ kind: 'error', message: 'message' in r ? r.message : t('admin.tenantAdmin.identityLoadError') });
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
        else setTenantState({ kind: 'error', message: 'message' in tr ? tr.message : t('admin.tenantAdmin.overviewLoadError') });
      });

      fetchTenantReadiness(cfg, tenantId).then(rr => {
        if (rr.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
        if (rr.kind === 'ok') setReadinessState({ kind: 'ok', report: rr.data });
        else setReadinessState({ kind: 'error', message: 'message' in rr ? rr.message : t('admin.readiness.loadError') });
      });
    });
  }, [apiBaseUrl, bearerToken]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => { load(); }, [load]);

  // AUTH012: refresh readiness alone after identity settings are saved, so the
  // IdentityConfig/RoleMapping checks update without remounting the whole page.
  const refreshReadiness = useCallback(() => {
    if (me.kind !== 'ready') return;
    fetchTenantReadiness(cfg, me.tenantId).then(rr => {
      if (rr.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (rr.kind === 'ok') setReadinessState({ kind: 'ok', report: rr.data });
      else setReadinessState({ kind: 'error', message: 'message' in rr ? rr.message : t('admin.readiness.loadError') });
    });
  }, [apiBaseUrl, bearerToken, me]); // eslint-disable-line react-hooks/exhaustive-deps

  if (me.kind === 'loading') return <p style={muted}>{t('common.loading')}</p>;
  if (me.kind === 'error') return (
    <div><p style={{ color: '#b91c1c' }}>{me.message}</p><button onClick={load} style={btn}>{t('admin.common.retry')}</button></div>
  );
  if (me.kind === 'ready' && !me.isAdmin) return (
    <p style={{ color: '#b91c1c' }}>{t('admin.tenantAdmin.noAccess')}</p>
  );

  const nextAction = readinessState.kind === 'ok'
    ? deriveNextAction(readinessState.kind === 'ok' ? readinessState.report.checks : [])
    : null;

  return (
    <div style={page}>
      <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>{t('admin.tenantAdmin.title')}</h2>

      {/* Tenant Overview */}
      <section style={card}>
        <h3 style={cardTitle}>{t('admin.tenantAdmin.overviewTitle')}</h3>
        {tenantState.kind === 'loading' && <p style={muted}>{t('common.loading')}</p>}
        {tenantState.kind === 'error' && tenantState.message && (
          <p style={{ color: '#b91c1c', fontSize: 13 }}>{tenantState.message}</p>
        )}
        {tenantState.kind === 'ok' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <Row label={t('admin.tenantAdmin.name')} value={tenantState.tenant.displayName} />
            <Row label={t('admin.tenantAdmin.slug')} value={tenantState.tenant.slug} />
            <Row label={t('admin.tenantAdmin.region')} value={tenantState.tenant.region} />
            <Row label={t('admin.tenantAdmin.timeZone')} value={tenantState.tenant.timeZone} />
            {/* PLAT007B — primary module drives the default landing experience. Only surface the
                additional-modules row when more than one is enabled (a single-module tenant needs
                no module selector). */}
            <Row label={t('admin.tenantAdmin.primaryModule')} value={tenantState.tenant.primaryModule} />
            {tenantState.tenant.enabledModules.length > 1 && (
              <Row label={t('admin.tenantAdmin.enabledModules')} value={tenantState.tenant.enabledModules.join(', ')} />
            )}
            <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
              <span style={rowLabel}>{t('admin.tenantAdmin.lifecycle')}</span>
              <LifecycleBadge state={tenantState.tenant.lifecycleState} />
            </div>
            <Row label={t('admin.tenantAdmin.created')} value={formatDate(new Date(tenantState.tenant.createdAt))} />
            <Row label={t('admin.tenantAdmin.updated')} value={formatDate(new Date(tenantState.tenant.updatedAt))} />
          </div>
        )}
      </section>

      {/* Readiness */}
      <section style={card}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 }}>
          <h3 style={{ ...cardTitle, marginBottom: 0 }}>{t('admin.readiness.title')}</h3>
          {readinessState.kind === 'ok' && (
            <ReadinessBadge isReady={readinessState.report.isReady} />
          )}
        </div>
        <p style={{ margin: '0 0 12px', fontSize: 12, color: '#6b7280' }}>
          {t('admin.readiness.description')}
        </p>

        {readinessState.kind === 'loading' && <p style={muted}>{t('common.loading')}</p>}
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

      {/* Identity & login settings (AUTH012) */}
      {me.kind === 'ready' && (
        <TenantIdentitySettingsSection
          cfg={cfg}
          tenantId={me.tenantId}
          onSaved={refreshReadiness}
          onUnauthenticated={() => { clear(); navigate('/session'); }}
        />
      )}

      {/* Next action */}
      {nextAction && (
        <section style={{ ...card, background: '#fffbeb', borderColor: '#fcd34d' }}>
          <h3 style={{ ...cardTitle, color: '#92400e' }}>{t('admin.readiness.actionRequired', { label: nextAction.label })}</h3>
          <p style={{ margin: '0 0 8px', fontSize: 13, color: '#78350f' }}>{nextAction.detail}</p>
          {nextAction.link && (
            <Link to={nextAction.link} className="btn-link" style={{ alignSelf: 'flex-start', marginLeft: -12 }}>
              {t('admin.readiness.goTo', { label: nextAction.linkLabel ?? nextAction.link })}
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
                ? t('admin.readiness.pilotReady')
                : t('admin.readiness.allPassed')}
            </p>
            {deferred.length > 0 && (
              <p style={{ margin: '6px 0 0', fontSize: 12, color: '#166534' }}>
                {tPlural('admin.readiness.deferredSummary', deferred.length, {
                  list: deferred.map(c => getCheckMeta(c.name).label).join(', '),
                })}
              </p>
            )}
          </section>
        );
      })()}

      <div style={{ display: 'flex', gap: 8 }}>
        <button onClick={load} style={btnSm}>{t('admin.common.refresh')}</button>
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
    Configured: { bg: '#eff6ff', text: 'var(--brand-primary)' },
    Seeded:     { bg: '#f0f9ff', text: '#0369a1' },
    Ready:      { bg: '#f0fdf4', text: '#166534' },
    Suspended:  { bg: '#fef9c3', text: '#854d0e' },
    Archived:   { bg: '#fef2f2', text: '#991b1b' },
  };
  const c = colors[state] ?? { bg: '#f3f4f6', text: '#374151' };
  return (
    <span style={{ background: c.bg, color: c.text, borderRadius: 99, padding: '2px 10px', fontSize: 12, fontWeight: 600 }}>
      {tDynamic('admin.tenantAdmin.lifecycleState', state, state)}
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
      {isReady ? t('admin.readiness.ready') : t('admin.readiness.notReady')}
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
            {status === 'Deferred' && <span style={{ fontWeight: 400, marginLeft: 4 }}>{t('admin.readiness.deferredSuffix')}</span>}
          </span>
          {meta.purpose && (
            <span style={{ fontSize: 12, color: '#6b7280' }}>{meta.purpose}</span>
          )}
        </div>
        {showNextAction && (
          <p style={{ margin: '4px 0 0', fontSize: 12, color: nextActionColor }}>
            {meta.nextAction}
            {meta.link && (
              <> — <Link to={meta.link} className="btn-link" style={{ fontSize: 12, minHeight: 38 }}>{t('admin.readiness.goTo', { label: meta.linkLabel ?? meta.link })}</Link></>
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
const btn: React.CSSProperties = { background: 'var(--brand-primary)', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
const btnSm: React.CSSProperties = { ...btn, padding: '6px 12px', fontSize: 13 };
