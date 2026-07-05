import type { components } from '@robertvejvoda/fairspot-api-client/booking';
import type { ApiClientConfig } from './client';

export type DrawStatusResponse = components['schemas']['DrawStatusResponse'];

export type DrawStatusResult =
  | { kind: 'ok'; data: DrawStatusResponse }
  | { kind: 'notFound' }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchDrawStatus(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  opts: { date: string; locationId: string; timeSlotStart: string; timeSlotEnd: string },
): Promise<DrawStatusResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };

  const params = new URLSearchParams({
    locationId: opts.locationId,
    timeSlotStart: `${opts.date}T${opts.timeSlotStart}`,
    timeSlotEnd: `${opts.date}T${opts.timeSlotEnd}`,
  });

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/draws/${opts.date}/status?${params}`, {
      headers: { Authorization: `Bearer ${bearerToken}` },
    });
  } catch (error) {
    return { kind: 'unreachable', message: error instanceof Error ? error.message : 'network error' };
  }

  if (response.status === 401 || response.status === 403) return { kind: 'unauthenticated' };
  if (response.status === 404) return { kind: 'notFound' };
  if (!response.ok) return { kind: 'error', status: response.status, message: `GET /draws status returned HTTP ${response.status}` };

  try {
    return { kind: 'ok', data: (await response.json()) as DrawStatusResponse };
  } catch {
    return { kind: 'error', status: response.status, message: 'Invalid response body.' };
  }
}
