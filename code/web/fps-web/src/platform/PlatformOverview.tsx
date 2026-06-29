import { useAuth } from '../auth/AuthContext';
import { isPlatformAdmin } from '../auth/roles';
import { NotWiredBadge } from './NotWiredBadge';

// Platform landing (platform-dashboard-ux.md §4). This shell slice renders the frame only:
// the red-flags strip and the Tenants / Onboarding / Activity summary cards are honest
// "Not wired yet" states naming the slice that will provide each source. No fake green/red
// operational status, and no real $ cost — the cost line is platform-admin-internal and shows
// "Not wired yet" even for admins in this slice.
export function PlatformOverview() {
  const { roles } = useAuth();
  const admin = isPlatformAdmin(roles);

  return (
    <div className="page-stack">
      <section className="page-hero">
        <div>
          <h2>Platform overview</h2>
          <p>FairSpot operator console — cross-tenant. Live data sources land in later slices.</p>
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
            Tenant state summary (Ready / Provisioning / Suspended / Archived) is backed by the
            Customer lifecycle API. The directory and counts land in PLAT008B.
          </p>
        </article>

        <article className="plat-card">
          <div className="plat-card-head">
            <h3>Onboarding</h3>
            <NotWiredBadge slice="PLAT008C" />
          </div>
          <p className="plat-muted">
            Intake counts come from the TenantRequest store. The triage queue lands in PLAT008C.
          </p>
        </article>

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
