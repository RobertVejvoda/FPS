import { useEffect, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { canTriagePlatformOnboarding, isPlatformAdmin } from '../auth/roles';
import { fetchTenantRequests, type TenantRequestStatus } from '../api/platform';
import { NotWiredBadge } from './NotWiredBadge';

type CountsState =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ok'; counts: Record<TenantRequestStatus, number> };

// Onboarding summary card — live request counts from the TenantRequest store (PLAT008C) for
// admins/operators. Auditors can't read prospect data, and any load failure falls back to an
// honest "unavailable" state rather than a fake figure.
function OnboardingCard() {
  const { apiBaseUrl, bearerToken, roles } = useAuth();
  const canTriage = canTriagePlatformOnboarding(roles);
  const [state, setState] = useState<CountsState>(canTriage ? { kind: 'loading' } : { kind: 'unavailable' });

  useEffect(() => {
    if (!canTriage) { setState({ kind: 'unavailable' }); return; }
    let active = true;
    void fetchTenantRequests({ apiBaseUrl, bearerToken }).then((r) => {
      if (!active) return;
      if (r.kind !== 'ok') { setState({ kind: 'unavailable' }); return; }
      const counts: Record<TenantRequestStatus, number> = { Requested: 0, Approved: 0, Rejected: 0 };
      for (const it of r.data) counts[it.status] += 1;
      setState({ kind: 'ok', counts });
    });
    return () => { active = false; };
  }, [canTriage, apiBaseUrl, bearerToken]);

  return (
    <article className="plat-card">
      <div className="plat-card-head">
        <h3>Onboarding</h3>
        {state.kind === 'ok' ? null : <NotWiredBadge availability="partial" slice="PLAT008C" />}
      </div>
      {state.kind === 'loading' && <p className="plat-muted">Loading request counts…</p>}
      {state.kind === 'ok' && (
        <p className="plat-muted">
          Requested <strong>{state.counts.Requested}</strong> · Approved <strong>{state.counts.Approved}</strong> · Rejected <strong>{state.counts.Rejected}</strong>.
          {' '}Triage them in the Onboarding queue.
        </p>
      )}
      {state.kind === 'unavailable' && (
        <p className="plat-muted">
          {canTriage
            ? 'Request counts are unavailable right now. Open the Onboarding queue to retry.'
            : 'Request counts hold prospect data and are restricted for platform_auditor.'}
        </p>
      )}
    </article>
  );
}

// Platform landing (platform-dashboard-ux.md §4). The overview mixes live platform slices with
// honest "not wired yet" states that name the slice that will provide each missing source. No
// fake green/red operational status, and no real $ cost until the usage ledger lands.
export function PlatformOverview() {
  const { roles } = useAuth();
  const admin = isPlatformAdmin(roles);

  return (
    <div className="page-stack">
      <section className="page-hero">
        <div>
          <h2>Platform overview</h2>
          <p>FairSpot operator console — cross-tenant. Live data appears as platform slices land.</p>
        </div>
      </section>

      <section className="plat-card">
        <div className="plat-card-head">
          <h3>Red flags</h3>
          <NotWiredBadge slice="PLAT008D" />
        </div>
        <p className="plat-muted">
          Operational health (Vault seal status, draw failures, boundary smoke, backups, demo
          staleness) is not wired in this slice. We never show a fake green/red status — these
          land with the health integrations (PLAT008D).
        </p>
      </section>

      <section className="plat-card-grid">
        <article className="plat-card">
          <div className="plat-card-head">
            <h3>Tenants</h3>
            <NotWiredBadge availability="partial" slice="PLAT008B" />
          </div>
          <p className="plat-muted">
            Tenant directory data is backed by the Customer lifecycle API. Aggregate state counts
            remain a later directory enhancement.
          </p>
        </article>

        <OnboardingCard />

        <article className="plat-card">
          <div className="plat-card-head">
            <h3>Activity (7d)</h3>
            <NotWiredBadge slice="PLAT005" />
          </div>
          <p className="plat-muted">
            Active users and draws come from the DataHub usage ledger (PLAT005). Figures are
            visible to all operators.
          </p>
          {admin ? (
            <p className="plat-cost-line">
              Cost (30d): <strong>$—</strong> <NotWiredBadge slice="PLAT005" />
              <span className="plat-admin-tag">platform_admin only</span>
            </p>
          ) : null}
        </article>
      </section>
    </div>
  );
}
