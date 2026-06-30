import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { canTriagePlatformOnboarding } from '../auth/roles';
import {
  decideTenantRequest,
  fetchTenantRequests,
  isDecidable,
  type TenantRequestItem,
  type TenantRequestStatus,
} from '../api/platform';

type State =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ok'; items: TenantRequestItem[] };

const GROUPS: { status: TenantRequestStatus; label: string }[] = [
  { status: 'Requested', label: 'Requested' },
  { status: 'Approved', label: 'Approved' },
  { status: 'Rejected', label: 'Rejected' },
];

// PLAT008C — platform onboarding queue. platform_admin/operator review tenant requests and
// approve/reject Requested items (with a reason); approval does NOT provision a tenant. The
// queue holds prospect PII, so platform_auditor gets a restricted state and never fetches it.
export function OnboardingQueuePage() {
  const { apiBaseUrl, bearerToken, roles } = useAuth();
  const canTriage = canTriagePlatformOnboarding(roles);

  const [state, setState] = useState<State>({ kind: 'loading' });
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busyId, setBusyId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setState({ kind: 'loading' });
    const r = await fetchTenantRequests({ apiBaseUrl, bearerToken });
    if (r.kind === 'ok') setState({ kind: 'ok', items: r.data });
    else if (r.kind === 'unauthenticated') setState({ kind: 'error', message: 'Your platform session is not authorized for the onboarding queue.' });
    else if (r.kind === 'unreachable') setState({ kind: 'error', message: 'Could not reach the platform API.' });
    else setState({ kind: 'error', message: r.message });
  }, [apiBaseUrl, bearerToken]);

  useEffect(() => {
    if (!canTriage) return;
    let active = true;
    void (async () => { if (active) await load(); })();
    return () => { active = false; };
  }, [canTriage, load]);

  const grouped = useMemo(() => {
    const map: Record<TenantRequestStatus, TenantRequestItem[]> = { Requested: [], Approved: [], Rejected: [] };
    if (state.kind === 'ok') for (const it of state.items) map[it.status].push(it);
    return map;
  }, [state]);

  async function decide(item: TenantRequestItem, action: 'approve' | 'reject') {
    setActionError(null);
    setBusyId(item.requestId);
    const r = await decideTenantRequest({ apiBaseUrl, bearerToken }, item.requestId, action, (reasons[item.requestId] ?? '').trim());
    setBusyId(null);
    if (r.kind === 'ok') {
      setReasons((m) => { const next = { ...m }; delete next[item.requestId]; return next; });
      await load();
      return;
    }
    if (r.kind === 'unauthenticated') setActionError('Your session is not authorized for that action.');
    else if (r.kind === 'unreachable') setActionError('Could not reach the platform API — please retry.');
    else setActionError(r.message);
  }

  if (!canTriage) {
    return (
      <section className="plat-page">
        <header className="plat-page-head"><h1>Onboarding queue</h1></header>
        <div className="plat-card" role="status">
          <h3>Restricted</h3>
          <p className="plat-muted">
            The onboarding queue holds prospect contact details. Triage is limited to
            <strong> platform_operator</strong> and <strong>platform_admin</strong>; as
            <strong> platform_auditor</strong> you can't view or action prospect information here.
          </p>
        </div>
      </section>
    );
  }

  return (
    <section className="plat-page">
      <header className="plat-page-head">
        <h1>Onboarding queue</h1>
        <p className="plat-muted">Review incoming tenant requests and approve or reject them. Approval records the decision only — it does not provision a tenant.</p>
      </header>

      {state.kind === 'loading' && <p className="plat-muted">Loading requests…</p>}
      {state.kind === 'error' && (
        <div>
          <p className="plat-error" role="alert">{state.message}</p>
          <button className="btn-secondary" onClick={() => { void load(); }}>Retry</button>
        </div>
      )}

      {state.kind === 'ok' && state.items.length === 0 && <p className="plat-muted">No tenant requests yet.</p>}

      {state.kind === 'ok' && state.items.length > 0 && (
        <>
          {actionError && <p className="plat-error" role="alert">{actionError}</p>}
          {GROUPS.map((g) => (
            <div key={g.status} className="plat-queue-group">
              <h2 className="plat-queue-head">{g.label} <span className="plat-muted">({grouped[g.status].length})</span></h2>
              {grouped[g.status].length === 0 ? (
                <p className="plat-muted plat-sub">None.</p>
              ) : (
                <div className="plat-card-grid">
                  {grouped[g.status].map((it) => (
                    <article key={it.requestId} className="plat-card">
                      <div className="plat-card-head">
                        <h3>{it.company}</h3>
                        <span className="plat-role-badge">{it.status}</span>
                      </div>
                      <dl className="plat-dl">
                        <dt>Domain</dt><dd>{it.primaryDomain}</dd>
                        <dt>Contact</dt><dd>{it.contactEmail}</dd>
                        <dt>Requested</dt><dd>{new Date(it.createdAt).toLocaleString()}</dd>
                        {it.message && (<><dt>Message</dt><dd>{it.message}</dd></>)}
                        {it.decidedAt && (<><dt>Decided</dt><dd>{new Date(it.decidedAt).toLocaleString()}</dd></>)}
                        {it.decisionReason && (<><dt>Reason</dt><dd>{it.decisionReason}</dd></>)}
                      </dl>
                      {isDecidable(it.status) && (
                        <div className="plat-queue-actions">
                          <input
                            className="plat-input"
                            placeholder="Reason (optional)"
                            value={reasons[it.requestId] ?? ''}
                            onChange={(e) => setReasons((m) => ({ ...m, [it.requestId]: e.target.value }))}
                            aria-label={`Decision reason for ${it.company}`}
                          />
                          <button className="btn-primary" disabled={busyId === it.requestId} onClick={() => { void decide(it, 'approve'); }}>Approve</button>
                          <button className="btn-secondary" disabled={busyId === it.requestId} onClick={() => { void decide(it, 'reject'); }}>Reject</button>
                        </div>
                      )}
                    </article>
                  ))}
                </div>
              )}
            </div>
          ))}
        </>
      )}
    </section>
  );
}
