import type { ApiClientConfig, FetchResult } from './client';

export interface HrDrawOutcomeItem {
  requestId: string;
  requestorRef: string;
  outcome: string;
  reasonCode: string | null;
  reason: string | null;
  allocatedSlotId: string | null;
}

export interface HrDrawOutcomeSummary {
  date: string;
  timeSlot: string;
  locationId: string | null;
  drawStatus: string;
  allocatedCount: number;
  rejectedCount: number;
  waitlistedCount: number;
  totalRequests: number;
  completedAt: string | null;
  outcomes: HrDrawOutcomeItem[];
}

export interface HrDrawOutcomesResponse {
  draws: HrDrawOutcomeSummary[];
}

export interface MyDrawOutcomeSummary {
  date: string;
  timeSlot: string;
  locationId: string | null;
  drawStatus: string;
  allocatedCount: number;
  totalRequests: number;
  completedAt: string | null;
  myOutcome: string;
  myReason: string | null;
  myAllocatedSlotId: string | null;
}

export interface MyDrawOutcomesResponse {
  draws: MyDrawOutcomeSummary[];
}

export async function fetchMyDrawOutcomes(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  params: { from?: string; to?: string } = {},
): Promise<FetchResult<MyDrawOutcomesResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const qs = new URLSearchParams();
  if (params.from) qs.set('from', params.from);
  if (params.to) qs.set('to', params.to);
  const url = `${apiBaseUrl}/draws/my-outcomes${qs.size ? `?${qs}` : ''}`;
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /draws/my-outcomes returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as MyDrawOutcomesResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchHrDrawOutcomes(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  params: { from?: string; to?: string; locationId?: string } = {},
): Promise<FetchResult<HrDrawOutcomesResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const qs = new URLSearchParams();
  if (params.from) qs.set('from', params.from);
  if (params.to) qs.set('to', params.to);
  if (params.locationId) qs.set('locationId', params.locationId);
  const url = `${apiBaseUrl}/draws/outcomes${qs.size ? `?${qs}` : ''}`;
  try {
    const res = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /draws/outcomes returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as HrDrawOutcomesResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
