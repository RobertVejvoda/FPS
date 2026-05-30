import type { components } from '@fps/api-client/booking';
import type { ApiClientConfig, FetchResult } from './client';

export type BookingListItem = components['schemas']['BookingListItem'];
export type GetMyBookingsResponse = components['schemas']['GetMyBookingsResponse'];
export type HrBookingListItem = components['schemas']['HrBookingListItem'];
export type GetHrBookingsResponse = components['schemas']['GetHrBookingsResponse'];
export type SubmitBookingRequest = components['schemas']['SubmitBookingRequest'];
export type SubmitBookingResponse = components['schemas']['SubmitBookingResponse'];
export type TriggerDrawRequest = components['schemas']['TriggerDrawRequest'];
export type TriggerDrawResponse = components['schemas']['TriggerDrawResponse'];

export type BookingsResult =
  | { kind: 'ok'; items: BookingListItem[]; nextCursor: string | null; totalCount: number }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type DrawStatusResult =
  | { kind: 'ok'; status: string; demandLevel: string; requestCount: number; canRequest: boolean; cannotRequestReason: string | null }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type SubmitResult =
  | { kind: 'accepted'; requestId: string; status: string }
  | { kind: 'rejected'; rejectionCode: string | null; reason: string | null }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type ActionResult =
  | { kind: 'ok' }
  | { kind: 'unauthenticated' }
  | { kind: 'notFound'; message: string }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type TriggerDrawResult =
  | { kind: 'accepted'; data: TriggerDrawResponse; wasAlreadyCompleted: boolean }
  | { kind: 'unauthenticated' }
  | { kind: 'forbidden'; message: string }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

async function readError(res: Response, fallback: string): Promise<string> {
  try {
    const d = (await res.json()) as { detail?: string; message?: string; title?: string };
    return d.detail ?? d.message ?? d.title ?? fallback;
  } catch {
    return fallback;
  }
}

export async function fetchBookings(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  opts?: { cursor?: string; from?: string; to?: string },
): Promise<BookingsResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const params = new URLSearchParams();
  if (opts?.cursor) params.set('cursor', opts.cursor);
  if (opts?.from) params.set('from', opts.from);
  if (opts?.to) params.set('to', opts.to);
  const query = params.toString();
  try {
    const res = await fetch(`${apiBaseUrl}/bookings${query ? `?${query}` : ''}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /bookings returned ${res.status}` };
    const data = (await res.json()) as GetMyBookingsResponse;
    return { kind: 'ok', items: data.items, nextCursor: data.nextCursor ?? null, totalCount: Number(data.totalCount ?? 0) };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function submitBooking(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  body: SubmitBookingRequest,
): Promise<SubmitResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/bookings`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(body),
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.status === 202 || res.status === 422) {
      const data = (await res.json()) as SubmitBookingResponse;
      if (res.status === 202) return { kind: 'accepted', requestId: data.requestId, status: data.status };
      return { kind: 'rejected', rejectionCode: data.rejectionCode, reason: data.reason };
    }
    return { kind: 'error', status: res.status, message: `POST /bookings returned ${res.status}` };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function cancelBooking(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  requestId: string,
): Promise<ActionResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/bookings/${encodeURIComponent(requestId)}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${bearerToken}` },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.status === 200) return { kind: 'ok' };
    if (res.status === 404) return { kind: 'notFound', message: await readError(res, 'Booking not found.') };
    return { kind: 'error', status: res.status, message: await readError(res, `DELETE /bookings returned ${res.status}`) };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function confirmUsage(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  requestId: string,
): Promise<FetchResult<{ wasAlreadyConfirmed: boolean }>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/bookings/${encodeURIComponent(requestId)}/confirm-usage`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ confirmationSource: 'EmployeeSelf' }),
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.status === 200) return { kind: 'ok', data: (await res.json()) as { wasAlreadyConfirmed: boolean } };
    if (res.status === 404) return { kind: 'error', status: 404, message: await readError(res, 'Booking not found.') };
    return { kind: 'error', status: res.status, message: `POST /confirm-usage returned ${res.status}` };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchDrawStatus(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  opts: { date: string; locationId: string; timeSlotStart: string; timeSlotEnd: string },
): Promise<DrawStatusResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const { date, locationId, timeSlotStart, timeSlotEnd } = opts;
    const params = new URLSearchParams({
      locationId,
      timeSlotStart: `${date}T${timeSlotStart}`,
      timeSlotEnd: `${date}T${timeSlotEnd}`,
    });
    const res = await fetch(`${apiBaseUrl}/draws/${date}/status?${params}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /draws/status returned ${res.status}` };
    const data = (await res.json()) as { status: string; demandLevel: string; requestCount: number | string; canRequest: boolean; cannotRequestReason: string | null };
    return { kind: 'ok', status: data.status, demandLevel: data.demandLevel, requestCount: Number(data.requestCount), canRequest: data.canRequest, cannotRequestReason: data.cannotRequestReason ?? null };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function triggerDraw(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  body: TriggerDrawRequest,
): Promise<TriggerDrawResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/draws/trigger`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(body),
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'forbidden', message: await readError(res, 'Only tenant admins can run a Draw.') };
    if (res.status === 200 || res.status === 202) {
      return { kind: 'accepted', data: (await res.json()) as TriggerDrawResponse, wasAlreadyCompleted: res.status === 200 };
    }
    return { kind: 'error', status: res.status, message: await readError(res, `POST /draws/trigger returned ${res.status}`) };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export type HrBookingsResult =
  | { kind: 'ok'; items: HrBookingListItem[]; nextCursor: string | null; totalCount: number }
  | { kind: 'unauthenticated' }
  | { kind: 'forbidden' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchHrBookings(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  opts?: { cursor?: string; locationId?: string; from?: string; to?: string; status?: string },
): Promise<HrBookingsResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const params = new URLSearchParams();
  if (opts?.cursor) params.set('cursor', opts.cursor);
  if (opts?.locationId) params.set('locationId', opts.locationId);
  if (opts?.from) params.set('from', opts.from);
  if (opts?.to) params.set('to', opts.to);
  if (opts?.status) params.set('status', opts.status);
  const query = params.toString();
  try {
    const res = await fetch(`${apiBaseUrl}/bookings/operations${query ? `?${query}` : ''}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'forbidden' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /bookings/operations returned ${res.status}` };
    const data = (await res.json()) as GetHrBookingsResponse;
    return { kind: 'ok', items: data.items, nextCursor: data.nextCursor ?? null, totalCount: Number(data.totalCount ?? 0) };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function hrCancelBooking(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  requestId: string,
  reason: string,
): Promise<ActionResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/bookings/${encodeURIComponent(requestId)}/hr-cancel`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${bearerToken}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason }),
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.status === 200) return { kind: 'ok' };
    if (res.status === 404) return { kind: 'notFound', message: await readError(res, 'Booking not found.') };
    return { kind: 'error', status: res.status, message: await readError(res, `DELETE /bookings/hr-cancel returned ${res.status}`) };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
