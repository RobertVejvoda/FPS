import type { components } from '@fps/api-client/booking';
import type { ApiClientConfig } from './client';

export type BookingListItem = components['schemas']['BookingListItem'];
export type GetMyBookingsResponse = components['schemas']['GetMyBookingsResponse'];

export type BookingsResult =
  | { kind: 'ok'; items: BookingListItem[]; nextCursor: string | null }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchBookings(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  cursor?: string,
): Promise<BookingsResult> {
  if (!apiBaseUrl || !bearerToken) {
    return { kind: 'unauthenticated' };
  }

  const params = new URLSearchParams();
  if (cursor) params.set('cursor', cursor);
  const query = params.toString();
  const url = `${apiBaseUrl}/bookings${query ? `?${query}` : ''}`;

  let response: Response;
  try {
    response = await fetch(url, {
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

  if (!response.ok) {
    return {
      kind: 'error',
      status: response.status,
      message: `Booking /bookings returned HTTP ${response.status}`,
    };
  }

  try {
    const data = (await response.json()) as GetMyBookingsResponse;
    return { kind: 'ok', items: data.items, nextCursor: data.nextCursor ?? null };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'invalid JSON';
    return { kind: 'error', status: response.status, message };
  }
}
