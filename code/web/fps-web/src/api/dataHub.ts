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
  slotId: string | null;
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
  // HR-supplied reason for manual / recovery runs (issue #472).
  runReason: string | null;
  // Operator-safe identifier of the actor that triggered the run.
  triggeredBy: string | null;
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

/** DRAW009: one ordered lifecycle step within the Draw progress read model. */
export interface DrawProgressStep {
  stepName: string;
  status: string;
  summary: string | null;
  occurredAt: string | null;
}

/**
 * DRAW009: Safe Draw workflow progress read model returned by
 * GET /datahub/draw-history/{drawAttemptId}/progress.
 * HR and auditor roles can see lifecycle steps once the Draw has completed.
 */
export interface DrawProgressResponse {
  drawAttemptId: string;
  locationId: string;
  date: string;
  timeSlot: string;
  status: string;
  triggerSource: string | null;
  runReason: string | null;
  triggeredBy: string | null;
  startedAt: string | null;
  completedAt: string | null;
  allocatedCount: number;
  rejectedCount: number;
  waitlistedCount: number;
  safeFailureReason: string | null;
  lastProjectedAt: string;
  /** Ordered lifecycle steps, or null when not yet available. See stepsNote. */
  steps: DrawProgressStep[] | null;
  /** Explains why steps may be null or incomplete. Null when steps are present. */
  stepsNote: string | null;
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

/**
 * AUD008: Auditor-safe booking-request detail read model returned by
 * GET /datahub/booking-requests/{bookingRequestId}/detail.
 * Contains only business-visible fields. No raw actor hashes, secrets,
 * algorithm seeds, candidate ordering, or scoring weights.
 */
export interface AuditorBookingRequestDetail {
  bookingRequestId: string;
  locationId: string;
  date: string;
  timeSlot: string;
  /** Current lifecycle status: Submitted, Allocated, Rejected, Cancelled, Used, NoShow, Expired, Waitlisted */
  status: string;
  reasonCode: string | null;
  safeReasonText: string | null;
  /** Allocation source when allocated: draw, sameDay, reallocation, manualCorrection */
  allocationSource: string | null;
  /** Allocated slot reference when request is allocated */
  slotId: string | null;
  /** Draw attempt ID when the request was decided via a Draw */
  drawAttemptId: string | null;
  submittedAt: string | null;
  decidedAt: string | null;
  /** When DataHub last updated this projection row */
  lastProjectedAt: string;
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

/**
 * DRAW009: Fetch Draw workflow progress (lifecycle steps) for a specific Draw attempt.
 * Returns one row with current status, counts, and ordered lifecycle steps.
 * Requires HR manager, admin, or auditor role.
 * Steps are available only after the Draw completes or fails; in-progress Draws return
 * status only with a stepsNote explaining the current state.
 */
export async function fetchDrawProgress(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  drawAttemptId: string,
): Promise<FetchResult<DrawProgressResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const url = `${apiBaseUrl}/datahub/draw-history/${encodeURIComponent(drawAttemptId)}/progress`;
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Draw attempt not found.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /datahub/draw-history/.../progress returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as DrawProgressResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

/**
 * AUD008: Fetch auditor-safe booking-request detail by booking request ID.
 * Returns business-safe fields scoped to the authenticated tenant.
 * Requires auditor or admin role.
 */
export async function fetchBookingRequestDetail(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  bookingRequestId: string,
): Promise<FetchResult<AuditorBookingRequestDetail>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const url = `${apiBaseUrl}/datahub/booking-requests/${encodeURIComponent(bookingRequestId)}/detail`;
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.status === 404) return { kind: 'error', status: 404, message: 'Booking request not found.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /datahub/booking-requests/${encodeURIComponent(bookingRequestId)}/detail returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as AuditorBookingRequestDetail };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
