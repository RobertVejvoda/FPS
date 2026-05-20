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
}

export async function fetchProfileSnapshot(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<ProfileSnapshot>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/profile/snapshot`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /profile/snapshot returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as ProfileSnapshot };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
