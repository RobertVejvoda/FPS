import type { ApiClientConfig, FetchResult } from './client';

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
  status: 'Passed' | 'Failed' | 'Skipped';
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
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /tenants returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as TenantResponse };
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
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /tenants/readiness returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as ReadinessReportResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
