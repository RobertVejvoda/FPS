import type { ApiClientConfig, FetchResult } from './client';

export interface ProfileSnapshot {
  tenantId: string;
  userId: string;
  profileStatus: string;
  parkingEligible: boolean;
  hasCompanyCar: boolean;
  accessibilityEligible: boolean;
  reservedSpaceEligible: boolean;
  vehicles: VehicleSnapshot[];
  snapshotVersion: string;
}

export interface VehicleSnapshot {
  vehicleId: string;
  licensePlate: string;
  vehicleType: string;
  isElectric: boolean;
  isActive: boolean;
  isDefault?: boolean;
}

export interface DisplayNamesResponse {
  names: Record<string, string | null>;
}

export async function fetchHrDisplayNames(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  userIds: string[],
): Promise<FetchResult<DisplayNamesResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  if (userIds.length === 0) return { kind: 'ok', data: { names: {} } };
  try {
    const res = await fetch(`${apiBaseUrl}/profile/hr/display-names`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ userIds }),
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `POST /profile/hr/display-names returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as DisplayNamesResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export interface RequestorVehicleSummary {
  licensePlate: string;
  vehicleType: string;
  isElectric: boolean;
  isDefault: boolean;
}

export interface RequestorSummary {
  userId: string;
  shortRef: string;
  displayName: string | null;
  profileStatus: string;
  parkingEligible: boolean;
  hasCompanyCar: boolean;
  accessibilityEligible: boolean;
  reservedSpaceEligible: boolean;
  activeVehicleCount: number;
  defaultVehicle: RequestorVehicleSummary | null;
}

export type RequestorSummaryResult =
  | { kind: 'ok'; data: RequestorSummary }
  | { kind: 'not-found'; userId: string; shortRef: string }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchHrRequestorSummary(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  userId: string,
): Promise<RequestorSummaryResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  if (!userId) return { kind: 'error', status: 400, message: 'userId is required.' };
  try {
    const res = await fetch(`${apiBaseUrl}/profile/hr/requestors/${encodeURIComponent(userId)}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.status === 404) {
      try {
        const body = await res.json() as { userId?: string; shortRef?: string };
        return { kind: 'not-found', userId: body.userId ?? userId, shortRef: body.shortRef ?? '' };
      } catch {
        return { kind: 'not-found', userId, shortRef: '' };
      }
    }
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /profile/hr/requestors/${userId} returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as RequestorSummary };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

// HR/admin-only eligibility update for allocation-impacting flags.
// Issue #481: company-car and accessibility eligibility must not be
// employee self-service. Either field may be omitted to leave it
// untouched; the server returns the resulting state so the drawer can
// reflect the change without a full reload.
export interface EligibilityUpdateRequest {
  hasCompanyCar?: boolean;
  accessibilityEligible?: boolean;
}

export interface EligibilityUpdateResponse {
  userId: string;
  shortRef: string;
  hasCompanyCar: boolean;
  accessibilityEligible: boolean;
  snapshotVersion: string;
  updatedAt: string;
}

export async function updateRequestorEligibility(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  userId: string,
  patch: EligibilityUpdateRequest,
): Promise<FetchResult<EligibilityUpdateResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/profile/hr/requestors/${encodeURIComponent(userId)}/eligibility`, {
      method: 'PATCH',
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(patch),
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Employee not found.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `PATCH eligibility returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as EligibilityUpdateResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

// Issue #533: per-location company-car employee counts used by the
// Configuration page to compute the company-car fixed-slot capacity warning
// against the existing slot list. HR/admin only on the server.
export interface CompanyCarLocationRow {
  locationId: string;
  companyCarEmployeeCount: number;
  companyCarUserIds: string[];
}

export interface CompanyCarLocationSummary {
  locations: CompanyCarLocationRow[];
}

export async function fetchCompanyCarLocationSummary(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<CompanyCarLocationSummary>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/profile/hr/company-car-locations`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /profile/hr/company-car-locations returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as CompanyCarLocationSummary };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchProfileSnapshot(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<ProfileSnapshot>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/profile/snapshot`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /profile/snapshot returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as ProfileSnapshot };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export type VehicleWriteResult =
  | { kind: 'ok'; vehicleId?: string }
  | { kind: 'unauthenticated' }
  | { kind: 'error'; status: number; message: string }
  | { kind: 'unreachable'; message: string };

async function vehicleRequest(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  method: string,
  path: string,
  body?: unknown,
): Promise<VehicleWriteResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}${path}`, {
      method,
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (!res.ok) {
      let message = `${method} ${path} returned ${res.status}`;
      try { const j = await res.json(); if (j?.error) message = j.error; } catch { /* ignore */ }
      return { kind: 'error', status: res.status, message };
    }
    if (res.status === 200) return { kind: 'ok', vehicleId: (await res.json()).vehicleId as string };
    return { kind: 'ok' };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export function addVehicle(
  cfg: ApiClientConfig,
  vehicle: { licensePlate: string; vehicleType: string; isElectric: boolean },
): Promise<VehicleWriteResult> {
  return vehicleRequest(cfg, 'POST', '/profile/vehicles', vehicle);
}

export function removeVehicle(cfg: ApiClientConfig, vehicleId: string): Promise<VehicleWriteResult> {
  return vehicleRequest(cfg, 'DELETE', `/profile/vehicles/${vehicleId}`);
}

export function setDefaultVehicle(cfg: ApiClientConfig, vehicleId: string): Promise<VehicleWriteResult> {
  return vehicleRequest(cfg, 'PUT', `/profile/vehicles/${vehicleId}/default`);
}
