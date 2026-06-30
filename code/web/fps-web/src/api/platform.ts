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
