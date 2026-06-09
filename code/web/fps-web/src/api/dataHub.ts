import type { ApiClientConfig, FetchResult } from './client';

export interface BookingOutcomeItem {
  bookingRequestId: string;
  locationId: string;
  date: string;
  timeSlot: string;
  finalStatus: string;
  reasonCode: string | null;
  safeReasonText: string | null;
  allocationSource: string | null;
  submittedAt: string | null;
  decidedAt: string | null;
}

export interface MyOutcomesResponse {
  items: BookingOutcomeItem[];
  page: number;
  pageSize: number;
  total: number;
}

export interface DrawHistoryItem {
  drawAttemptId: string;
  locationId: string;
  date: string;
  timeSlot: string;
  status: string;
  triggerSource: string | null;
  startedAt: string | null;
  completedAt: string | null;
  allocatedCount: number;
  rejectedCount: number;
  waitlistedCount: number;
  safeFailureReason: string | null;
}

export interface DrawHistoryResponse {
  items: DrawHistoryItem[];
  page: number;
  pageSize: number;
  total: number;
}

export interface DrawOutcomeItem {
  bookingRequestId: string;
  requestorId: string;
  locationId: string;
  date: string;
  timeSlot: string;
  finalStatus: string;
  reasonCode: string | null;
  safeReasonText: string | null;
  allocationSource: string | null;
  slotId: string | null;
  decidedAt: string | null;
}

export interface DrawOutcomesResponse {
  draw: {
    drawAttemptId: string;
    locationId: string;
    date: string;
    timeSlot: string;
    status: string;
    allocatedCount: number;
    rejectedCount: number;
    waitlistedCount: number;
    completedAt: string | null;
  };
  outcomes: DrawOutcomeItem[];
  page: number;
  pageSize: number;
  total: number;
}

export interface ProjectionHealthResponse {
  lastDrawUpdate: string | null;
  lastOutcomeUpdate: string | null;
  lastProcessedEventAt: string | null;
  processingLagSeconds: number | null;
  pendingEvents: number;
  failedEvents: number;
  poisonedEvents: number;
  status: string;
}

export async function fetchMyOutcomes(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  params: { fromDate?: string; toDate?: string; page?: number; pageSize?: number } = {},
): Promise<FetchResult<MyOutcomesResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const qs = new URLSearchParams();
  if (params.fromDate) qs.set('fromDate', params.fromDate);
  if (params.toDate) qs.set('toDate', params.toDate);
  if (params.page) qs.set('page', String(params.page));
  if (params.pageSize) qs.set('pageSize', String(params.pageSize));
  const url = `${apiBaseUrl}/datahub/my-outcomes${qs.size ? `?${qs}` : ''}`;
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /datahub/my-outcomes returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as MyOutcomesResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchDrawHistory(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  params: { locationId?: string; fromDate?: string; toDate?: string; page?: number; pageSize?: number } = {},
): Promise<FetchResult<DrawHistoryResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const qs = new URLSearchParams();
  if (params.locationId) qs.set('locationId', params.locationId);
  if (params.fromDate) qs.set('fromDate', params.fromDate);
  if (params.toDate) qs.set('toDate', params.toDate);
  if (params.page) qs.set('page', String(params.page));
  if (params.pageSize) qs.set('pageSize', String(params.pageSize));
  const url = `${apiBaseUrl}/datahub/draw-history${qs.size ? `?${qs}` : ''}`;
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /datahub/draw-history returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as DrawHistoryResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchDrawOutcomes(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  drawAttemptId: string,
  params: { page?: number; pageSize?: number } = {},
): Promise<FetchResult<DrawOutcomesResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const qs = new URLSearchParams();
  if (params.page) qs.set('page', String(params.page));
  if (params.pageSize) qs.set('pageSize', String(params.pageSize));
  const url = `${apiBaseUrl}/datahub/draw-outcomes/${encodeURIComponent(drawAttemptId)}${qs.size ? `?${qs}` : ''}`;
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Draw not found.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /datahub/draw-outcomes returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as DrawOutcomesResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchProjectionHealth(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<ProjectionHealthResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const url = `${apiBaseUrl}/datahub/projection-health`;
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /datahub/projection-health returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as ProjectionHealthResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
