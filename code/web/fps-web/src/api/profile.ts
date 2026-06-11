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
