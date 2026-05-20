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

export interface SlotDto {
  slotId: string;
  isActive: boolean;
  hasCharger: boolean;
  isAccessible: boolean;
  isCompanyCarOnly: boolean;
  isMotorcycleCapacity: boolean;
  reservedForUserId: string | null;
}

export async function fetchParkingPolicy(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<ParkingPolicy>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/configuration/parking-policy`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
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
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ ...policy, publicationReason: null }),
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.ok) return { kind: 'ok', data: {} };
    return { kind: 'error', status: res.status, message: `PUT /configuration/parking-policy returned ${res.status}` };
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
    const res = await fetch(`${apiBaseUrl}/configuration/locations/${encodeURIComponent(locationId)}/slots`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /configuration/locations slots returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as SlotDto[] };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
