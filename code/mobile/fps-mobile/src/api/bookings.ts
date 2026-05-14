import type { components } from '@fps/api-client/booking';
import type { ApiClientConfig } from './client';

export type BookingListItem = components['schemas']['BookingListItem'];
export type GetMyBookingsResponse = components['schemas']['GetMyBookingsResponse'];
export type SubmitBookingRequest = components['schemas']['SubmitBookingRequest'];
export type SubmitBookingResponse = components['schemas']['SubmitBookingResponse'];
export type ConfirmUsageRequest = components['schemas']['ConfirmUsageRequest'];
export type ConfirmUsageResponse = components['schemas']['ConfirmUsageResponse'];

export type SubmitBookingResult =
  | { kind: 'accepted'; requestId: string; status: string }
  | { kind: 'rejected'; rejectionCode: string | null; reason: string | null }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type CancelBookingResult =
  | { kind: 'ok' }
  | { kind: 'unauthenticated' }
  | { kind: 'notFound'; message: string }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type ConfirmUsageResult =
  | { kind: 'confirmed'; wasAlreadyConfirmed: boolean }
  | { kind: 'unauthenticated' }
  | { kind: 'notFound'; message: string }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type BookingsResult =
  | { kind: 'ok'; items: BookingListItem[]; nextCursor: string | null }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchBookings(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  opts?: { cursor?: string; from?: string; to?: string },
): Promise<BookingsResult> {
  if (!apiBaseUrl || !bearerToken) {
    return { kind: 'unauthenticated' };
  }

  const params = new URLSearchParams();
  if (opts?.cursor) params.set('cursor', opts.cursor);
  if (opts?.from) params.set('from', opts.from);
  if (opts?.to) params.set('to', opts.to);
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

export async function submitBooking(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  body: SubmitBookingRequest,
): Promise<SubmitBookingResult> {
  if (!apiBaseUrl || !bearerToken) {
    return { kind: 'unauthenticated' };
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/bookings`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${bearerToken}`,
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(body),
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : 'network error';
    return { kind: 'unreachable', message };
  }

  if (response.status === 401 || response.status === 403) {
    return { kind: 'unauthenticated' };
  }

  if (response.status === 202 || response.status === 422) {
    try {
      const data = (await response.json()) as SubmitBookingResponse;
      if (response.status === 202) {
        return { kind: 'accepted', requestId: data.requestId, status: data.status };
      }
      return { kind: 'rejected', rejectionCode: data.rejectionCode, reason: data.reason };
    } catch {
      return { kind: 'error', status: response.status, message: 'Invalid response body.' };
    }
  }

  return {
    kind: 'error',
    status: response.status,
    message: `POST /bookings returned HTTP ${response.status}`,
  };
}

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  try {
    const data = (await response.json()) as { detail?: string; title?: string; message?: string };
    return data.detail ?? data.message ?? data.title ?? fallback;
  } catch {
    return fallback;
  }
}

export async function cancelBooking(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  requestId: string,
  reason?: string,
): Promise<CancelBookingResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };

  const params = reason ? `?reason=${encodeURIComponent(reason)}` : '';
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/bookings/${encodeURIComponent(requestId)}${params}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${bearerToken}` },
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : 'network error';
    return { kind: 'unreachable', message };
  }

  if (response.status === 401 || response.status === 403) return { kind: 'unauthenticated' };
  if (response.status === 200) return { kind: 'ok' };
  if (response.status === 404) {
    return {
      kind: 'notFound',
      message: await readErrorMessage(response, 'This booking no longer exists.'),
    };
  }
  return {
    kind: 'error',
    status: response.status,
    message: await readErrorMessage(response, `DELETE /bookings returned HTTP ${response.status}`),
  };
}

export async function confirmBookingUsage(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  requestId: string,
): Promise<ConfirmUsageResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/bookings/${encodeURIComponent(requestId)}/confirm-usage`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${bearerToken}`,
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify({ confirmationSource: 'EmployeeSelf' } satisfies ConfirmUsageRequest),
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : 'network error';
    return { kind: 'unreachable', message };
  }

  if (response.status === 401 || response.status === 403) return { kind: 'unauthenticated' };
  if (response.status === 200) {
    try {
      const data = (await response.json()) as ConfirmUsageResponse;
      return { kind: 'confirmed', wasAlreadyConfirmed: data.wasAlreadyConfirmed };
    } catch {
      return { kind: 'error', status: 200, message: 'Invalid response body.' };
    }
  }
  if (response.status === 404) {
    return {
      kind: 'notFound',
      message: await readErrorMessage(response, 'This booking no longer exists.'),
    };
  }
  return {
    kind: 'error',
    status: response.status,
    message: await readErrorMessage(response, `POST /confirm-usage returned HTTP ${response.status}`),
  };
}
