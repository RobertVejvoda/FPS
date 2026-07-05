import type { components } from '@robertvejvoda/fairspot-api-client/profile';
import type { ApiClientConfig } from './client';

export type ProfileSnapshot = components['schemas']['ProfileSnapshot'];
export type VehicleSnapshot = components['schemas']['VehicleSnapshot'];

export type ProfileSnapshotResult =
  | { kind: 'ok'; profile: ProfileSnapshot }
  | { kind: 'unauthenticated' }
  | { kind: 'notFound' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchProfileSnapshot({
  apiBaseUrl,
  bearerToken,
}: ApiClientConfig): Promise<ProfileSnapshotResult> {
  if (!apiBaseUrl || !bearerToken) {
    return { kind: 'unauthenticated' };
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/profile/snapshot`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${bearerToken}`,
        Accept: 'application/json',
      },
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : 'network error';
    return { kind: 'unreachable', message };
  }

  if (response.status === 401 || response.status === 403) {
    return { kind: 'unauthenticated' };
  }

  if (response.status === 404) {
    return { kind: 'notFound' };
  }

  if (!response.ok) {
    return {
      kind: 'error',
      status: response.status,
      message: `Profile /profile/snapshot returned HTTP ${response.status}`,
    };
  }

  try {
    const profile = (await response.json()) as ProfileSnapshot;
    return { kind: 'ok', profile };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'invalid JSON';
    return { kind: 'error', status: response.status, message };
  }
}
