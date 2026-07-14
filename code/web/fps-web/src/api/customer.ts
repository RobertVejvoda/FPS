import type { components } from '@robertvejvoda/fairspot-api-client/customer';
import type { ApiClientConfig, FetchResult } from './client';

export interface TenantDiscoveryResponse {
  slug: string;
  displayName: string;
  // 'LocalAccount' | 'CompanySso' | 'Both' — routing hint only, never authorization.
  loginMode: string;
  // Keycloak identity-provider broker alias for this tenant's company SSO, when one is
  // configured (tenant branding IdpAlias, AUTH010). Passed as kc_idp_hint for
  // CompanySso tenants only — never for Both, so local fallback stays reachable.
  idpAlias?: string | null;
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

// Public "Start a FairSpot Pilot" evaluation request (PLAT004c). Anonymous POST — the
// endpoint is bot-protected and rate-limited at the edge; no bearer token is sent and no
// prospect data is persisted client-side. Typed against the generated client so the wire
// shape (company / primaryDomain / contactEmail / message / turnstileToken) stays in sync.
type SubmitPilotBody = components['schemas']['SubmitTenantRequest'];
type PilotAcknowledgement = components['schemas']['TenantRequestAcknowledgement'];

export interface PilotRequestInput {
  companyName: string;
  companyDomain: string;
  workEmail: string;
  message: string;
  verificationToken: string;
}

export type PilotRequestResult =
  | { kind: 'ok'; reference: string }
  // Field-level problem surfaced by the API as a human-readable message we can show as-is.
  | { kind: 'invalid'; message: string }
  // Too many requests from this network in a short window.
  | { kind: 'rate-limited' }
  | { kind: 'error' };

export async function submitPilotRequest(
  apiBaseUrl: string,
  input: PilotRequestInput,
): Promise<PilotRequestResult> {
  const body: SubmitPilotBody = {
    company: input.companyName,
    primaryDomain: input.companyDomain,
    contactEmail: input.workEmail,
    message: input.message,
    turnstileToken: input.verificationToken || null,
  };
  try {
    const res = await fetch(`${apiBaseUrl}/tenant-requests`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(body),
    });
    if (res.status === 202) {
      const data = (await res.json()) as PilotAcknowledgement;
      return { kind: 'ok', reference: data.requestId };
    }
    if (res.status === 429) return { kind: 'rate-limited' };
    if (res.status === 400) {
      return { kind: 'invalid', message: await readProblemDetail(res) };
    }
    return { kind: 'error' };
  } catch {
    return { kind: 'error' };
  }
}

async function readProblemDetail(res: Response): Promise<string> {
  try {
    const problem = (await res.json()) as { detail?: string | null; title?: string | null };
    return problem.detail || problem.title || 'Please check your details and try again.';
  } catch {
    return 'Please check your details and try again.';
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
  // PLAT007B — the tenant's primary module (default landing / navigation emphasis) and every
  // enabled module (primary first). Business-readable names, e.g. "Parking", "Seats".
  primaryModule: string;
  enabledModules: string[];
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

// PLAT-seats (#710) — which modules the signed-in tenant runs, so the tenant app can decide
// whether to show a module switch (only when more than one module is enabled). Readable by any
// authenticated member of the tenant.
export interface TenantModulesResponse {
  primaryModule: string;
  enabledModules: string[];
}

export async function fetchTenantModules(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  tenantId: string,
): Promise<FetchResult<TenantModulesResponse>> {
  if (!apiBaseUrl || !bearerToken || !tenantId) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/tenants/${encodeURIComponent(tenantId)}/modules`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /tenants/{tenantId}/modules returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as TenantModulesResponse };
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
