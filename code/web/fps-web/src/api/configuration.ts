import type { ApiClientConfig, FetchResult } from './client';

export interface ParkingPolicy {
  tenantId: string;
  locationId: string | null;
  timeZone: string;
  drawCutOffTime: string;
  dailyRequestCap: number;
  allocationLookbackDays: number;
  lateCancellationPenalty: number;
  noShowPenalty: number;
  manualAdjustmentEnabled: boolean;
  sameDayBookingEnabled: boolean;
  sameDayUsesRequestCap: boolean;
  automaticReallocationEnabled: boolean;
  usageConfirmationRequired: boolean;
  usageConfirmationWindowMinutes: number;
  usageConfirmationMethods: string[];
  noShowDetectionEnabled: boolean;
  companyCarTier1Enabled: boolean;
  companyCarOverflowBehavior: string;
  version: string;
}

export interface PolicyHistoryItem {
  version: string;
  publishedAt: string;
  publishedByHash: string | null;
  publicationReason: string | null;
}

export interface SlotDto {
  slotId: string;
  isActive: boolean;
  hasCharger: boolean;
  isAccessible: boolean;
  isCompanyCarOnly: boolean;
  isMotorcycleCapacity: boolean;
  // Configurable per-slot motorcycle capacity. Null means "use the default"
  // (4 in v1) when the slot is motorcycle-specific. Ignored otherwise.
  motorcycleCapacityUnits: number | null;
  reservedForUserId: string | null;
}

export interface SlotHistoryItem {
  version: string;
  changedAt: string;
  changedByHash: string | null;
  changeReason: string | null;
  slotCount: number;
}

function auth(bearerToken: string) {
  return { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' };
}

function handle401403<T>(status: number): FetchResult<T> | null {
  if (status === 401) return { kind: 'unauthenticated' };
  if (status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
  return null;
}

export async function fetchParkingPolicy(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<ParkingPolicy>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/parking-policy`, { headers: auth(bearerToken) });
    const early = handle401403<ParkingPolicy>(res.status); if (early) return early;
    if (res.status === 404) return { kind: 'error', status: 404, message: 'No policy configured for this tenant.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /configuration/parking-policy returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as ParkingPolicy };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function saveParkingPolicy(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  policy: Omit<ParkingPolicy, 'tenantId' | 'version'>,
): Promise<FetchResult<Record<string, never>>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/parking-policy`, {
      method: 'PUT',
      headers: { ...auth(bearerToken), 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...policy, publicationReason: null }),
    });
    const early = handle401403<Record<string, never>>(res.status); if (early) return early;
    if (res.ok) return { kind: 'ok', data: {} };
    return { kind: 'error', status: res.status, message: `PUT /configuration/parking-policy returned ${res.status}` };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchLocationPolicy(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  locationId: string,
): Promise<FetchResult<ParkingPolicy>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/locations/${encodeURIComponent(locationId)}/parking-policy`, { headers: auth(bearerToken) });
    const early = handle401403<ParkingPolicy>(res.status); if (early) return early;
    if (res.status === 404) return { kind: 'error', status: 404, message: 'No location policy configured.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /configuration/locations policy returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as ParkingPolicy };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function saveLocationPolicy(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  locationId: string,
  policy: Omit<ParkingPolicy, 'tenantId' | 'version'>,
): Promise<FetchResult<Record<string, never>>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/locations/${encodeURIComponent(locationId)}/parking-policy`, {
      method: 'PUT',
      headers: { ...auth(bearerToken), 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...policy, publicationReason: null }),
    });
    const early = handle401403<Record<string, never>>(res.status); if (early) return early;
    if (res.ok) return { kind: 'ok', data: {} };
    return { kind: 'error', status: res.status, message: `PUT /configuration/locations policy returned ${res.status}` };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchPolicyHistory(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<PolicyHistoryItem[]>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/parking-policy/history`, { headers: auth(bearerToken) });
    const early = handle401403<PolicyHistoryItem[]>(res.status); if (early) return early;
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /configuration/parking-policy/history returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as PolicyHistoryItem[] };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchSlots(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  locationId: string,
): Promise<FetchResult<SlotDto[]>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/locations/${encodeURIComponent(locationId)}/slots`, { headers: auth(bearerToken) });
    const early = handle401403<SlotDto[]>(res.status); if (early) return early;
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /configuration/locations slots returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as SlotDto[] };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

// Public-safe slot map projection. Open to any authenticated tenant user;
// ReservedForUserId is never returned (only the boolean `isReserved`).
export interface SlotMapDto {
  slotId: string;
  isActive: boolean;
  hasCharger: boolean;
  isAccessible: boolean;
  isCompanyCarOnly: boolean;
  isMotorcycleCapacity: boolean;
  // Resolved number of motorcycles that fit on a motorcycle-specific slot
  // (default 4 when isMotorcycleCapacity=true). Always 1 for non-motorcycle slots.
  motorcycleCapacityUnits: number;
  isReserved: boolean;
}

export async function fetchSlotMap(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  locationId: string,
): Promise<FetchResult<SlotMapDto[]>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/locations/${encodeURIComponent(locationId)}/slots/map`, { headers: auth(bearerToken) });
    const early = handle401403<SlotMapDto[]>(res.status); if (early) return early;
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /configuration/locations slots/map returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as SlotMapDto[] };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchSlotHistory(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  locationId: string,
): Promise<FetchResult<SlotHistoryItem[]>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/locations/${encodeURIComponent(locationId)}/slots/history`, { headers: auth(bearerToken) });
    const early = handle401403<SlotHistoryItem[]>(res.status); if (early) return early;
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /configuration/locations slots/history returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as SlotHistoryItem[] };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchLocationPolicyHistory(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  locationId: string,
): Promise<FetchResult<PolicyHistoryItem[]>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/locations/${encodeURIComponent(locationId)}/parking-policy/history`, { headers: auth(bearerToken) });
    const early = handle401403<PolicyHistoryItem[]>(res.status); if (early) return early;
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /configuration/locations parking-policy/history returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as PolicyHistoryItem[] };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function saveSlots(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  locationId: string,
  slots: SlotDto[],
  changeReason: string | null,
): Promise<FetchResult<Record<string, never>>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/locations/${encodeURIComponent(locationId)}/slots`, {
      method: 'PUT',
      headers: { ...auth(bearerToken), 'Content-Type': 'application/json' },
      body: JSON.stringify({ slots, changeReason }),
    });
    const early = handle401403<Record<string, never>>(res.status); if (early) return early;
    if (res.ok) return { kind: 'ok', data: {} };
    return { kind: 'error', status: res.status, message: `PUT /configuration/locations slots returned ${res.status}` };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
