import type { ApiClientConfig, FetchResult } from './client';

export interface TenantDiscoveryResponse {
  slug: string;
  displayName: string;
  loginMode: string;
  primaryColor?: string;
  accentColor?: string;
  logoAssetId?: string;
  faviconAssetId?: string;
  legalFooterText?: string;
}

export type DiscoverResult =
  | { kind: 'ok'; data: TenantDiscoveryResponse }
  | { kind: 'notfound' }
  | { kind: 'error' };

export async function discoverTenant(
  apiBaseUrl: string,
  domain: string,
): Promise<DiscoverResult> {
  try {
    const res = await fetch(
      `${apiBaseUrl}/tenants/discover?domain=${encodeURIComponent(domain)}`,
      { headers: { Accept: 'application/json' } },
    );
    if (res.status === 404) return { kind: 'notfound' };
    if (!res.ok) return { kind: 'error' };
    return { kind: 'ok', data: (await res.json()) as TenantDiscoveryResponse };
  } catch {
    return { kind: 'error' };
  }
}

export interface TenantContactDto {
  name: string;
  email: string;
  role: string;
}

export interface TenantResponse {
  tenantId: string;
  slug: string;
  displayName: string;
  region: string;
  timeZone: string;
  lifecycleState: string;
  supportContacts: TenantContactDto[];
  serviceCollections: Record<string, string>;
  createdAt: string;
  updatedAt: string;
}

export interface ReadinessCheckDto {
  name: string;
  status: 'Passed' | 'Failed' | 'Skipped' | 'Deferred';
  reason: string | null;
}

export interface ReadinessReportResponse {
  tenantId: string;
  isDryRun: boolean;
  isReady: boolean;
  checks: ReadinessCheckDto[];
}

export async function fetchTenant(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  tenantId: string,
): Promise<FetchResult<TenantResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/tenants/${encodeURIComponent(tenantId)}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Tenant not found.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /tenants/{tenantId} returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as TenantResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

// Tenant-level parking bootstrap — used by the Configuration page (#477)
// to auto-discover known locations instead of asking HR to type a Location
// id into a free-text box.
export interface TenantBootstrapLocationDto {
  locationId: string;
  activeSlotCount: number;
  hasLocationPolicy: boolean;
  isUsable: boolean;
  recordedByHash: string | null;
  recordedAt: string | null;
}

export interface TenantBootstrapResponse {
  tenantId: string;
  defaultPolicyConfigured: boolean;
  hasUsableLocation: boolean;
  isComplete: boolean;
  locations: TenantBootstrapLocationDto[];
}

export async function fetchTenantParkingBootstrap(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  tenantId: string,
): Promise<FetchResult<TenantBootstrapResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/tenants/${encodeURIComponent(tenantId)}/parking-bootstrap`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Tenant not found.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /tenants/{tenantId}/parking-bootstrap returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as TenantBootstrapResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchTenantReadiness(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  tenantId: string,
): Promise<FetchResult<ReadinessReportResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/tenants/${encodeURIComponent(tenantId)}/readiness`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Tenant not found.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /tenants/{tenantId}/readiness returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as ReadinessReportResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
