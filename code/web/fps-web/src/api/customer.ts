import type { components } from '@robertvejvoda/fairspot-api-client/customer';
import type { ApiClientConfig, FetchResult } from './client';

export interface TenantDiscoveryResponse {
  slug: string;
  displayName: string;
  loginMode: string;
  // AUTH011 (#793): non-secret Keycloak broker alias, returned only for CompanySso
  // tenants with a configured broker. Passed to Keycloak as kc_idp_hint so the
  // browser skips the account chooser. Routing metadata only — never access.
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

// AUTH012 (#795) — tenant-admin identity settings. The generated customer client types
// the ConfigureIdentityRequest body but not the GET response content (the endpoint
// returns an untyped IActionResult), so the response shape is typed locally here and
// kept in sync with IdentityConfigResponse in TenantIdentityController.cs.
export interface TenantIdentityConfigResponse {
  tenantId: string;
  trustedIssuer: string;
  audience: string;
  tenantClaimName: string;
  subjectClaimName: string;
  roleClaimNames: string[];
  roleMapping: Record<string, string>;
  localAccountPolicyEnabled: boolean;
  // Non-secret Keycloak broker alias (AUTH011): routing metadata for kc_idp_hint,
  // never a client secret and never access authority.
  idpBrokerAlias: string | null;
  configuredByHash: string;
  configuredAt: string;
  updatedAt: string | null;
}

export interface TenantIdentityConfigInput {
  trustedIssuer: string;
  audience: string;
  tenantClaimName: string;
  subjectClaimName: string;
  roleClaimNames: string[];
  roleMapping: Record<string, string>;
  localAccountPolicyEnabled: boolean;
  idpBrokerAlias: string | null;
}

// 404 is a normal state here — a tenant that has never configured identity gets an
// empty/default setup form, not an error page.
export type IdentityConfigResult =
  | { kind: 'ok'; data: TenantIdentityConfigResponse }
  | { kind: 'notconfigured' }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchTenantIdentityConfig(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  tenantId: string,
): Promise<IdentityConfigResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/tenants/${encodeURIComponent(tenantId)}/identity-config`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.status === 404) return { kind: 'notconfigured' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /tenants/{tenantId}/identity-config returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as TenantIdentityConfigResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export type SaveIdentityConfigResult =
  | { kind: 'ok' }
  // Server-side validation problem in business-readable form (e.g. the IdpBrokerAlias
  // format message) — shown to the admin as-is.
  | { kind: 'invalid'; message: string }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function saveTenantIdentityConfig(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  tenantId: string,
  input: TenantIdentityConfigInput,
): Promise<SaveIdentityConfigResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/tenants/${encodeURIComponent(tenantId)}/identity-config`, {
      method: 'PUT',
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({
        trustedIssuer: input.trustedIssuer,
        audience: input.audience,
        tenantClaimName: input.tenantClaimName,
        subjectClaimName: input.subjectClaimName,
        roleClaimNames: input.roleClaimNames,
        roleMapping: input.roleMapping,
        idpBrokerAlias: input.idpBrokerAlias,
        localAccountPolicyEnabled: input.localAccountPolicyEnabled,
      }),
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Tenant not found.' };
    if (res.status === 400) {
      const problem = (await res.json().catch(() => null)) as { error?: string } | null;
      return { kind: 'invalid', message: problem?.error ?? 'Please check the identity settings and try again.' };
    }
    if (!res.ok) return { kind: 'error', status: res.status, message: `PUT /tenants/{tenantId}/identity-config returned ${res.status}` };
    return { kind: 'ok' };
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
