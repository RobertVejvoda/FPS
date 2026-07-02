import type { ApiClientConfig, FetchResult } from './client';

// Platform-plane tenant directory/detail (PLAT008B). These endpoints are platform-only and
// deliberately excluded from the open @fps/api-client (ApiExplorerSettings.IgnoreApi), so the
// platform surface types them locally and calls them directly.

export type PlatformTenantRow = {
  tenantId: string;
  slug: string;
  displayName: string;
  region: string;
  timeZone: string;
  kind: string;
  lifecycleState: string;
  // PLAT007B — primary module (default landing / navigation emphasis) and all enabled modules
  // (primary first), for operator visibility. Business-readable names, e.g. "Parking", "Seats".
  primaryModule: string;
  enabledModules: string[];
  createdAt: string;
  updatedAt: string;
};

export type PlatformReadinessCheck = { name: string; status: string; reason: string | null };
export type PlatformReadiness = { isReady: boolean; checks: PlatformReadinessCheck[] };
export type PlatformIdentity = {
  trustedIssuer: string;
  audience: string;
  roleClaimNames: string[];
  roleMapping: Record<string, string>;
  localAccountPolicyEnabled: boolean;
};
export type PlatformTransition = { from: string; to: string; actorId: string; occurredAt: string; reason: string | null };
export type SupportContact = { name: string; email: string; role: string };

export type PlatformTenantDetail = {
  overview: PlatformTenantRow;
  supportContacts: SupportContact[];
  loginMode: string;
  discoveryDomains: string[];
  readiness: PlatformReadiness | null;
  identity: PlatformIdentity | null;
  lifecycleHistory: PlatformTransition[];
};

async function getJson<T>({ apiBaseUrl, bearerToken }: ApiClientConfig, path: string): Promise<FetchResult<T>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}${path}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Not found.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET ${path} returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as T };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

async function postJson<T>({ apiBaseUrl, bearerToken }: ApiClientConfig, path: string, body: unknown): Promise<FetchResult<T>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}${path}`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Not found.' };
    if (!res.ok) {
      const detail = await res.json().catch(() => null);
      return { kind: 'error', status: res.status, message: (detail?.error as string) ?? `POST ${path} returned ${res.status}` };
    }
    return { kind: 'ok', data: (await res.json().catch(() => ({}))) as T };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export const fetchPlatformTenants = (cfg: ApiClientConfig) =>
  getJson<PlatformTenantRow[]>(cfg, '/platform/tenants');

export const fetchPlatformTenantDetail = (cfg: ApiClientConfig, tenantId: string) =>
  getJson<PlatformTenantDetail>(cfg, `/platform/tenants/${encodeURIComponent(tenantId)}`);

// PLAT008C — platform onboarding queue (tenant requests). Same direct-fetch reasoning as the
// tenant directory: GET/POST /tenant-requests are platform-only and excluded from the open client.

export type TenantRequestStatus = 'Requested' | 'Approved' | 'Rejected';

// The backend serialises the status enum as a number (0/1/2) by default; tolerate a string too
// so the queue is robust if a JsonStringEnumConverter is added later.
export function normalizeRequestStatus(raw: number | string): TenantRequestStatus {
  if (raw === 1 || raw === 'Approved') return 'Approved';
  if (raw === 2 || raw === 'Rejected') return 'Rejected';
  return 'Requested';
}

// Only business-relevant fields. DecidedByHash is intentionally NOT included — raw actor hashes
// must never reach the UI (PLAT008C).
export type TenantRequestItem = {
  requestId: string;
  company: string;
  primaryDomain: string;
  contactEmail: string;
  message: string;
  status: TenantRequestStatus;
  createdAt: string;
  decidedAt: string | null;
  decisionReason: string | null;
};

type TenantRequestWire = Omit<TenantRequestItem, 'status'> & { status: number | string };

export async function fetchTenantRequests(cfg: ApiClientConfig): Promise<FetchResult<TenantRequestItem[]>> {
  const result = await getJson<TenantRequestWire[]>(cfg, '/tenant-requests');
  if (result.kind !== 'ok') return result;
  const items: TenantRequestItem[] = result.data.map((r) => ({
    requestId: r.requestId,
    company: r.company,
    primaryDomain: r.primaryDomain,
    contactEmail: r.contactEmail,
    message: r.message,
    status: normalizeRequestStatus(r.status),
    createdAt: r.createdAt,
    decidedAt: r.decidedAt ?? null,
    decisionReason: r.decisionReason ?? null,
  }));
  return { kind: 'ok', data: items };
}

export const decideTenantRequest = (cfg: ApiClientConfig, requestId: string, action: 'approve' | 'reject', reason: string) =>
  postJson<unknown>(cfg, `/tenant-requests/${encodeURIComponent(requestId)}/${action}`, { reason });

// Only Requested items are actionable; an already-decided request can't be re-decided from the UI.
export const isDecidable = (status: TenantRequestStatus): boolean => status === 'Requested';

// ── PLAT008D — platform health strip sources ────────────────────────────────
// Health is normalised into four honest states. `ok` / `warning` are only ever derived from a
// live source; `unavailable` means the source exists but the read failed; `not-wired` means no
// safe source is backing this signal yet (never a fake green/red).
export type HealthStatus = 'ok' | 'warning' | 'unavailable' | 'not-wired';

// PLAT005A — aggregate-only per-tenant monthly usage stats (DataHub, platform-role gated). No PII;
// only counts. Serialised camelCase by the DataHub controller. We consume the activity subset.
export type PlatformUsageRow = {
  tenantId: string;
  period: string;
  activeRequestorCount: number;
  bookingRequestCount: number;
  drawRunCount: number;
  allocatedCount: number;
  rejectedCount: number;
  cancelledCount: number;
  expiredCount: number;
  noShowCount: number;
  usedCount: number;
  lastUpdatedAt: string;
};

// PLAT003A/B — last sandbox-reset evidence (Customer, platform-reader gated). Safe fields only:
// status/source/timestamps/snapshot version/aggregate purge counts. The raw failureReason can
// carry internal wording, so the UI derives status from it but never renders it verbatim.
export type SandboxResetEvidence = {
  tenantId: string;
  status: string;
  source: string;
  startedAt: string;
  completedAt: string | null;
  snapshotVersion: string | null;
  failureReason: string | null;
};

// GET /datahub/platform/usage-stats?month=YYYY-MM — all platform roles may read (auditor included).
export const fetchPlatformUsageStats = (cfg: ApiClientConfig, month: string) =>
  getJson<PlatformUsageRow[]>(cfg, `/datahub/platform/usage-stats?month=${encodeURIComponent(month)}`);

// GET /platform/tenants/{id}/reset-sandbox — 404 when no reset has ever run for the sandbox.
export const fetchSandboxResetEvidence = (cfg: ApiClientConfig, tenantId: string) =>
  getJson<SandboxResetEvidence>(cfg, `/platform/tenants/${encodeURIComponent(tenantId)}/reset-sandbox`);

// Current calendar month in the ledger's YYYY-MM period key, in UTC to match the server.
export function currentMonthKey(now: Date = new Date()): string {
  return `${now.getUTCFullYear()}-${String(now.getUTCMonth() + 1).padStart(2, '0')}`;
}

// Tenant readiness rollup from the live directory. Suspended tenants are the only red flag here;
// Draft/Configured/Seeded are normal onboarding stages, and Archived tenants are intentionally
// retired (not a warning).
export type TenantReadiness = { total: number; ready: number; suspended: number; byState: Record<string, number> };

export function summarizeTenantReadiness(rows: PlatformTenantRow[]): TenantReadiness {
  const byState: Record<string, number> = {};
  for (const r of rows) byState[r.lifecycleState] = (byState[r.lifecycleState] ?? 0) + 1;
  return { total: rows.length, ready: byState['Ready'] ?? 0, suspended: byState['Suspended'] ?? 0, byState };
}

export function tenantReadinessStatus(s: TenantReadiness): HealthStatus {
  if (s.total === 0) return 'ok';
  return s.suspended > 0 ? 'warning' : 'ok';
}

// Activity rollup across all tenants for a month.
export type UsageSummary = { activeRequestors: number; bookingRequests: number; drawRuns: number; tenantsWithActivity: number };

export function summarizeUsage(rows: PlatformUsageRow[]): UsageSummary {
  const summary: UsageSummary = { activeRequestors: 0, bookingRequests: 0, drawRuns: 0, tenantsWithActivity: 0 };
  for (const r of rows) {
    summary.activeRequestors += r.activeRequestorCount;
    summary.bookingRequests += r.bookingRequestCount;
    summary.drawRuns += r.drawRunCount;
    if (r.activeRequestorCount + r.bookingRequestCount + r.drawRunCount > 0) summary.tenantsWithActivity += 1;
  }
  return summary;
}

// The sandbox tenant is identified by its stored kind, so the console never hard-codes a slug/id.
export function findSandboxTenant(rows: PlatformTenantRow[]): PlatformTenantRow | null {
  return rows.find((r) => r.kind === 'Sandbox') ?? null;
}

// A successful last reset is OK regardless of age (no scheduled reset is live yet, so staleness is
// not itself a red flag); any non-success outcome is a warning the operator should look into.
export function sandboxFreshnessStatus(ev: SandboxResetEvidence): HealthStatus {
  return ev.status === 'Succeeded' ? 'ok' : 'warning';
}

// Coarse relative age for freshness copy ("today" / "3 days ago"). Whole days keeps the UI honest
// without implying second-level precision.
export function formatRelativeAge(iso: string, now: Date = new Date()): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return 'unknown';
  const days = Math.floor((now.getTime() - then) / 86_400_000);
  if (days <= 0) return 'today';
  if (days === 1) return 'yesterday';
  return `${days} days ago`;
}
