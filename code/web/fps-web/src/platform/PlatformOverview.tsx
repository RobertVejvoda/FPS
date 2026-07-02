import { HealthStrip } from './HealthStrip';

// Platform landing (platform-dashboard-ux.md §4). The overview now leads with the operator health
// strip (PLAT008D) — live tenant readiness, onboarding, and DataHub activity, an honest sandbox
// freshness signal, and explicit "not wired yet" cards for signals with no safe source yet. No
// fake green/red operational status, and no real $ cost.
export function PlatformOverview() {
  return (
    <div className="page-stack">
      <section className="page-hero">
        <div>
          <h2>Platform overview</h2>
          <p>FairSpot operator console — cross-tenant. Live data appears as platform slices land.</p>
        </div>
      </section>

      <HealthStrip />
    </div>
  );
}
