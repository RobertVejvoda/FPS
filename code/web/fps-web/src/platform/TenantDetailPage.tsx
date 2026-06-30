import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchPlatformTenantDetail, type PlatformTenantDetail } from '../api/platform';

type State =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ok'; detail: PlatformTenantDetail };

type Tab = 'overview' | 'config' | 'identity' | 'lifecycle' | 'audit';
const TABS: { id: Tab; label: string }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'config', label: 'Config' },
  { id: 'identity', label: 'Identity / role mapping' },
  { id: 'lifecycle', label: 'Lifecycle history' },
  { id: 'audit', label: 'Audit links' },
];

function NotWired({ reason }: { reason: string }) {
  return <p className="plat-na" title={reason}>Not wired yet — {reason}</p>;
}

// PLAT008B — read-only platform tenant detail. No mutating actions (no suspend/archive/edit).
// Overview/Identity/Lifecycle are live from the Customer API; Usage/cost/modules/demo/feedback
// are explicit "Not wired yet".
export function TenantDetailPage() {
  const { tenantId = '' } = useParams();
  const { apiBaseUrl, bearerToken } = useAuth();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [tab, setTab] = useState<Tab>('overview');

  useEffect(() => {
    let active = true;
    setState({ kind: 'loading' });
    void fetchPlatformTenantDetail({ apiBaseUrl, bearerToken }, tenantId).then((r) => {
      if (!active) return;
      if (r.kind === 'ok') setState({ kind: 'ok', detail: r.data });
      else if (r.kind === 'unauthenticated') setState({ kind: 'error', message: 'Your platform session is not authorized.' });
      else if (r.kind === 'unreachable') setState({ kind: 'error', message: 'Could not reach the platform API.' });
      else setState({ kind: 'error', message: r.status === 404 ? 'Tenant not found.' : r.message });
    });
    return () => { active = false; };
  }, [apiBaseUrl, bearerToken, tenantId]);

  return (
    <section className="plat-page">
      <p><Link to="/platform/tenants">← Tenant directory</Link></p>

      {state.kind === 'loading' && <p className="plat-muted">Loading tenant…</p>}
      {state.kind === 'error' && <p className="plat-error" role="alert">{state.message}</p>}

      {state.kind === 'ok' && (() => {
        const d = state.detail;
        return (
          <>
            <header className="plat-page-head">
              <h1>{d.overview.displayName}</h1>
              <p className="plat-muted">{d.overview.lifecycleState} · {d.overview.region || 'region —'} · {d.overview.kind}</p>
            </header>

            <div className="plat-tabs" role="tablist">
              {TABS.map((t) => (
                <button key={t.id} role="tab" aria-selected={tab === t.id}
                  className={`plat-tab${tab === t.id ? ' plat-tab-active' : ''}`}
                  onClick={() => setTab(t.id)}>{t.label}</button>
              ))}
            </div>

            {tab === 'overview' && (
              <div className="plat-card-grid">
                <div className="plat-card">
                  <h3>Tenant</h3>
                  <dl className="plat-dl">
                    <dt>Slug</dt><dd>{d.overview.slug}</dd>
                    <dt>Lifecycle state</dt><dd>{d.overview.lifecycleState}</dd>
                    <dt>Region / time zone</dt><dd>{d.overview.region || '—'} / {d.overview.timeZone || '—'}</dd>
                    <dt>Login mode</dt><dd>{d.loginMode}</dd>
                    <dt>Created</dt><dd>{new Date(d.overview.createdAt).toLocaleString()}</dd>
                    <dt>Updated</dt><dd>{new Date(d.overview.updatedAt).toLocaleString()}</dd>
                  </dl>
                </div>
                <div className="plat-card">
                  <h3>Readiness</h3>
                  {d.readiness ? (
                    <>
                      <p>{d.readiness.isReady ? '✅ Ready' : '◐ Not ready'}</p>
                      <ul className="plat-checks">
                        {d.readiness.checks.map((c) => (
                          <li key={c.name}>{c.name}: <strong>{c.status}</strong>{c.reason ? ` — ${c.reason}` : ''}</li>
                        ))}
                      </ul>
                    </>
                  ) : <p className="plat-muted">No readiness report.</p>}
                </div>
                <div className="plat-card">
                  <h3>Support contacts</h3>
                  {d.supportContacts.length > 0 ? (
                    <ul className="plat-checks">{d.supportContacts.map((c, i) => <li key={i}>{c.name} ({c.role}) — {c.email}</li>)}</ul>
                  ) : <p className="plat-muted">None recorded.</p>}
                </div>
                <div className="plat-card">
                  <h3>Usage &amp; cost <span className="plat-admin-tag">platform_admin only</span></h3>
                  <NotWired reason="usage/cost ledger (PLAT005)" />
                </div>
                <div className="plat-card">
                  <h3>Modules</h3>
                  <NotWired reason="module licensing (PLAT007)" />
                </div>
              </div>
            )}

            {tab === 'config' && (
              <div className="plat-card">
                <h3>Configuration</h3>
                <dl className="plat-dl">
                  <dt>Login mode</dt><dd>{d.loginMode}</dd>
                  <dt>Discovery domains</dt><dd>{d.discoveryDomains.length > 0 ? d.discoveryDomains.join(', ') : '—'}</dd>
                </dl>
                <p className="plat-muted">Read-only here. Policy/slot configuration is edited in tenant settings (audited); deeper config view is partial.</p>
              </div>
            )}

            {tab === 'identity' && (
              <div className="plat-card">
                <h3>Identity / role mapping</h3>
                {d.identity ? (
                  <dl className="plat-dl">
                    <dt>Trusted issuer</dt><dd>{d.identity.trustedIssuer || '—'}</dd>
                    <dt>Audience</dt><dd>{d.identity.audience || '—'}</dd>
                    <dt>Role claim(s)</dt><dd>{d.identity.roleClaimNames.join(', ') || '—'}</dd>
                    <dt>Role mapping</dt><dd>{Object.entries(d.identity.roleMapping).map(([k, v]) => `${k} → ${v}`).join('; ') || '—'}</dd>
                    <dt>Local accounts</dt><dd>{d.identity.localAccountPolicyEnabled ? 'enabled' : 'disabled'}</dd>
                  </dl>
                ) : <p className="plat-muted">Identity not configured for this tenant yet.</p>}
              </div>
            )}

            {tab === 'lifecycle' && (
              <div className="plat-card">
                <h3>Lifecycle history</h3>
                {d.lifecycleHistory.length > 0 ? (
                  <ul className="plat-checks">
                    {d.lifecycleHistory.map((t, i) => (
                      <li key={i}>{new Date(t.occurredAt).toLocaleString()}: <strong>{t.from} → {t.to}</strong>{t.reason ? ` — ${t.reason}` : ''}</li>
                    ))}
                  </ul>
                ) : <p className="plat-muted">No transitions recorded.</p>}
                <p className="plat-muted">Read-only — lifecycle actions are not available in this view.</p>
              </div>
            )}

            {tab === 'audit' && (
              <div className="plat-card">
                <h3>Audit links</h3>
                <p className="plat-muted">Deep cross-tenant audit view is wired in a later slice.</p>
                <NotWired reason="platform audit deep view" />
              </div>
            )}
          </>
        );
      })()}
    </section>
  );
}
