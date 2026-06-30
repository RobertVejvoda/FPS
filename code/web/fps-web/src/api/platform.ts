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

export const fetchPlatformTenants = (cfg: ApiClientConfig) =>
  getJson<PlatformTenantRow[]>(cfg, '/platform/tenants');

export const fetchPlatformTenantDetail = (cfg: ApiClientConfig, tenantId: string) =>
  getJson<PlatformTenantDetail>(cfg, `/platform/tenants/${encodeURIComponent(tenantId)}`);
