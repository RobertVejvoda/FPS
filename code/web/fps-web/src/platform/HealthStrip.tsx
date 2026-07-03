import { useEffect, useState, type ReactNode } from 'react';
import { useAuth } from '../auth/AuthContext';
import { canTriagePlatformOnboarding } from '../auth/roles';
import {
  currentMonthKey,
  drawHealthStatus,
  fetchPlatformDrawHealth,
  fetchPlatformTenants,
  fetchPlatformUsageStats,
  fetchSandboxResetEvidence,
  fetchTenantRequests,
  findSandboxTenant,
  formatRelativeAge,
  sandboxFreshnessStatus,
  summarizeTenantReadiness,
  summarizeUsage,
  tenantReadinessStatus,
  type HealthStatus,
  type PlatformDrawHealth,
  type TenantRequestStatus,
} from '../api/platform';
import { HealthStatusPill } from './HealthStatusPill';

// PLAT008D — operator-facing platform health strip (platform-dashboard-ux.md §4). Replaces the old
// "red flags not wired" placeholder. Every card names its source and shows one of OK / Warning /
// Unavailable / Not wired yet. Live cards derive status only from real reads; a failed read is an
// honest "Unavailable"; a signal with no safe source is "Not wired yet". No fake green/red, and no
// $ cost, secrets, hostnames, actor hashes, requestor ids, or infrastructure internals reach here.

type CardProps = { title: string; status: HealthStatus; source: string; children: ReactNode };

function HealthCard({ title, status, source, children }: CardProps) {
  return (
    <article className="plat-card health-card">
      <div className="plat-card-head">
        <h3>{title}</h3>
        <HealthStatusPill status={status} />
      </div>
      <div className="health-card-body">{children}</div>
      <p className="health-source">Source: {source}</p>
    </article>
  );
}

// ── Tenant readiness (live — Customer directory) ─────────────────────────────
type TenantState =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ok'; status: HealthStatus; total: number; ready: number; suspended: number; byState: Record<string, number> };

function TenantReadinessCard() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [state, setState] = useState<TenantState>({ kind: 'loading' });

  useEffect(() => {
    let active = true;
    void fetchPlatformTenants({ apiBaseUrl, bearerToken }).then((r) => {
      if (!active) return;
      if (r.kind !== 'ok') { setState({ kind: 'unavailable' }); return; }
      const summary = summarizeTenantReadiness(r.data);
      setState({ kind: 'ok', status: tenantReadinessStatus(summary), ...summary });
    });
    return () => { active = false; };
  }, [apiBaseUrl, bearerToken]);

  const status: HealthStatus = state.kind === 'ok' ? state.status : state.kind === 'loading' ? 'ok' : 'unavailable';
  return (
    <HealthCard title="Tenant readiness" status={status} source="Customer tenant directory">
      {state.kind === 'loading' && <p className="plat-muted">Loading tenant states…</p>}
      {state.kind === 'unavailable' && <p className="plat-muted">Tenant directory is unreachable right now. Open the Tenants page to retry.</p>}
      {state.kind === 'ok' && state.total === 0 && <p className="plat-muted">No tenants provisioned yet.</p>}
      {state.kind === 'ok' && state.total > 0 && (
        <p className="plat-muted">
          <strong>{state.ready}</strong> of <strong>{state.total}</strong> tenants Ready
          {state.suspended > 0 ? <> · <strong>{state.suspended}</strong> Suspended — needs attention.</> : '. No suspended tenants.'}
        </p>
      )}
    </HealthCard>
  );
}

// ── Onboarding (live/partial — TenantRequest queue) ──────────────────────────
type OnboardingState =
  | { kind: 'loading' }
  | { kind: 'restricted' }
  | { kind: 'unavailable' }
  | { kind: 'ok'; counts: Record<TenantRequestStatus, number> };

function OnboardingCard() {
  const { apiBaseUrl, bearerToken, roles } = useAuth();
  const canTriage = canTriagePlatformOnboarding(roles);
  const [state, setState] = useState<OnboardingState>(canTriage ? { kind: 'loading' } : { kind: 'restricted' });

  useEffect(() => {
    if (!canTriage) { setState({ kind: 'restricted' }); return; }
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

  // A pending queue is normal operations, not a red flag — the card stays OK and reports the count.
  // Auditors can't read prospect data, so they see a safe restricted state rather than a fake value.
  const status: HealthStatus =
    state.kind === 'ok' ? 'ok' : state.kind === 'restricted' ? 'not-wired' : state.kind === 'loading' ? 'ok' : 'unavailable';

  return (
    <HealthCard title="Onboarding" status={status} source="Tenant request queue">
      {state.kind === 'loading' && <p className="plat-muted">Loading request counts…</p>}
      {state.kind === 'unavailable' && <p className="plat-muted">Request counts are unavailable right now. Open the Onboarding queue to retry.</p>}
      {state.kind === 'restricted' && <p className="plat-muted">Request counts hold prospect data and are restricted for platform_auditor.</p>}
      {state.kind === 'ok' && (
        <p className="plat-muted">
          <strong>{state.counts.Requested}</strong> pending · {state.counts.Approved} approved · {state.counts.Rejected} rejected.
          {state.counts.Requested > 0 ? ' Triage them in the Onboarding queue.' : ' Nothing waiting.'}
        </p>
      )}
    </HealthCard>
  );
}

// ── Activity this month (live — DataHub usage ledger) ────────────────────────
type ActivityState =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ok'; activeRequestors: number; bookingRequests: number; drawRuns: number; tenantsWithActivity: number };

function ActivityCard() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const month = currentMonthKey();
  const [state, setState] = useState<ActivityState>({ kind: 'loading' });

  useEffect(() => {
    let active = true;
    void fetchPlatformUsageStats({ apiBaseUrl, bearerToken }, month).then((r) => {
      if (!active) return;
      if (r.kind !== 'ok') { setState({ kind: 'unavailable' }); return; }
      setState({ kind: 'ok', ...summarizeUsage(r.data) });
    });
    return () => { active = false; };
  }, [apiBaseUrl, bearerToken, month]);

  const status: HealthStatus = state.kind === 'ok' ? 'ok' : state.kind === 'loading' ? 'ok' : 'unavailable';
  return (
    <HealthCard title={`Activity (${month})`} status={status} source="DataHub usage ledger">
      {state.kind === 'loading' && <p className="plat-muted">Loading usage stats…</p>}
      {state.kind === 'unavailable' && <p className="plat-muted">Usage stats are unavailable right now.</p>}
      {state.kind === 'ok' && (state.activeRequestors + state.bookingRequests + state.drawRuns) === 0 && (
        <p className="plat-muted">No platform activity recorded yet this month.</p>
      )}
      {state.kind === 'ok' && (state.activeRequestors + state.bookingRequests + state.drawRuns) > 0 && (
        <p className="plat-muted">
          <strong>{state.activeRequestors}</strong> active requestors · <strong>{state.bookingRequests}</strong> booking requests · <strong>{state.drawRuns}</strong> draws
          {' '}across <strong>{state.tenantsWithActivity}</strong> tenant{state.tenantsWithActivity === 1 ? '' : 's'}. Aggregate counts only — no cost.
        </p>
      )}
    </HealthCard>
  );
}

// ── Demo sandbox freshness (live — sandbox reset evidence) ───────────────────
type SandboxState =
  | { kind: 'loading' }
  | { kind: 'no-sandbox' }
  | { kind: 'no-evidence' }
  | { kind: 'unavailable' }
  | { kind: 'ok'; status: HealthStatus; statusLabel: string; source: string; freshness: string };

function SandboxCard() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [state, setState] = useState<SandboxState>({ kind: 'loading' });

  useEffect(() => {
    let active = true;
    void (async () => {
      const tenants = await fetchPlatformTenants({ apiBaseUrl, bearerToken });
      if (!active) return;
      if (tenants.kind !== 'ok') { setState({ kind: 'unavailable' }); return; }
      const sandbox = findSandboxTenant(tenants.data);
      if (!sandbox) { setState({ kind: 'no-sandbox' }); return; }
      const ev = await fetchSandboxResetEvidence({ apiBaseUrl, bearerToken }, sandbox.tenantId);
      if (!active) return;
      if (ev.kind === 'error' && ev.status === 404) { setState({ kind: 'no-evidence' }); return; }
      if (ev.kind !== 'ok') { setState({ kind: 'unavailable' }); return; }
      const when = ev.data.completedAt ?? ev.data.startedAt;
      setState({
        kind: 'ok',
        status: sandboxFreshnessStatus(ev.data),
        statusLabel: ev.data.status,
        source: ev.data.source,
        freshness: formatRelativeAge(when),
      });
    })();
    return () => { active = false; };
  }, [apiBaseUrl, bearerToken]);

  const status: HealthStatus =
    state.kind === 'ok' ? state.status
      : state.kind === 'unavailable' ? 'unavailable'
        : state.kind === 'loading' ? 'ok'
          : 'not-wired';

  return (
    <HealthCard title="Demo sandbox freshness" status={status} source="Sandbox reset evidence">
      {state.kind === 'loading' && <p className="plat-muted">Checking last reset…</p>}
      {state.kind === 'no-sandbox' && <p className="plat-muted">No sandbox tenant is registered. This signal wires up once an evaluation sandbox exists.</p>}
      {state.kind === 'no-evidence' && <p className="plat-muted">Sandbox tenant found, but no reset has been recorded yet.</p>}
      {state.kind === 'unavailable' && <p className="plat-muted">Sandbox reset evidence is unavailable right now.</p>}
      {state.kind === 'ok' && (
        <p className="plat-muted">
          Last reset <strong>{state.statusLabel}</strong> ({state.source}), {state.freshness}.
        </p>
      )}
    </HealthCard>
  );
}

// ── Draw health (live — DataHub draw history) ────────────────────────────────
type DrawHealthState =
  | { kind: 'loading' }
  | { kind: 'unavailable' }
  | { kind: 'ok'; data: PlatformDrawHealth };

function DrawHealthCard() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [state, setState] = useState<DrawHealthState>({ kind: 'loading' });

  useEffect(() => {
    let active = true;
    void fetchPlatformDrawHealth({ apiBaseUrl, bearerToken }).then((r) => {
      if (!active) return;
      if (r.kind !== 'ok') { setState({ kind: 'unavailable' }); return; }
      setState({ kind: 'ok', data: r.data });
    });
    return () => { active = false; };
  }, [apiBaseUrl, bearerToken]);

  const status: HealthStatus =
    state.kind === 'ok' ? drawHealthStatus(state.data)
      : state.kind === 'loading' ? 'ok'
        : 'unavailable';

  return (
    <HealthCard title="Draw health" status={status} source="DataHub draw history">
      {state.kind === 'loading' && <p className="plat-muted">Checking recent draws…</p>}
      {state.kind === 'unavailable' && <p className="plat-muted">Draw health is unavailable right now.</p>}
      {state.kind === 'ok' && !state.data.hasEvidence && (
        <p className="plat-muted">No draw projection evidence recorded yet — draw health can&rsquo;t be confirmed.</p>
      )}
      {state.kind === 'ok' && state.data.hasEvidence && (() => {
        const d = state.data;
        const clean = d.failedCount === 0 && d.stuckCount === 0 && !d.stale;
        return (
          <p className="plat-muted">
            Last {d.windowDays}d: <strong>{d.completedCount}</strong> completed
            {d.failedCount > 0 ? <> · <strong>{d.failedCount}</strong> failed</> : null}
            {d.stuckCount > 0 ? <> · <strong>{d.stuckCount}</strong> stuck (any age)</> : null}
            {clean
              ? '. No failed or stuck draws.'
              : d.stale && d.failedCount === 0 && d.stuckCount === 0
                ? ' — no recent draw activity; projection may be stale.'
                : ' — needs attention.'}
            {d.lastActivityAt ? <> Last activity {formatRelativeAge(d.lastActivityAt)}.</> : null}
          </p>
        );
      })()}
    </HealthCard>
  );
}

// ── Not-wired-yet operational signals ────────────────────────────────────────
// Hosted-boundary evidence has no platform-plane read source yet — it's a release smoke artifact
// (see PLAT008E). We keep it an honest "not wired yet" rather than inventing a green it can't back.
function NotWiredCard({ title, source, children }: { title: string; source: string; children: ReactNode }) {
  return (
    <HealthCard title={title} status="not-wired" source={source}>
      <p className="plat-muted">{children}</p>
    </HealthCard>
  );
}

export function HealthStrip() {
  return (
    <section className="page-stack">
      <div className="health-strip-head">
        <h3>Platform health</h3>
        <p className="plat-muted">
          Operator-facing signals. Each card names its source and shows an honest state — we never
          fake a green or red status. Raw infrastructure detail stays in Grafana / ops runbooks.
        </p>
      </div>
      <div className="plat-card-grid">
        <TenantReadinessCard />
        <OnboardingCard />
        <ActivityCard />
        <DrawHealthCard />
        <SandboxCard />
        <NotWiredCard title="Hosted boundary" source="Release smoke evidence">
          Public app/auth reachability and the internal-exposure boundary are verified by the hosted
          smoke as a release artifact, not yet a platform-plane read — so this stays not wired yet
          rather than showing a green it can&rsquo;t back. See the{' '}
          <a href="https://github.com/RobertVejvoda/fairspot/blob/master/docs/production/hosted-smoke-runbook.md"
             target="_blank" rel="noopener noreferrer">hosted smoke runbook</a>.
        </NotWiredCard>
      </div>
    </section>
  );
}
