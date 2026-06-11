import type { ApiClientConfig, FetchResult } from './client';

export type ActivityCategory =
  | 'All'
  | 'BookingLifecycle'
  | 'DrawEvents'
  | 'PolicyChanges'
  | 'Notifications'
  | 'PrivacyErasure'
  | 'ManualCorrections';

export interface AuditRecord {
  auditRecordId: string;
  sourceEventId: string;
  eventType: string;
  eventVersion: number;
  occurredAt: string;
  recordedAt: string;
  correlationId: string;
  causationId: string | null;
  actorType: string;
  actorHash: string | null;
  source: string;
  entityType: string;
  entityId: string | null;
  // Business activity timeline fields (AUD006, AUDIT003)
  action: string;
  result: string | null;
  reasonCode: string | null;
  summary: string | null;
  traceId: string | null;
}

export interface AuditListResponse {
  items: AuditRecord[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditQueryFilters {
  eventType?: string;
  entityType?: string;
  entityId?: string;
  actorHash?: string;
  actorRef?: string; // Short prefix (6 hex chars) — matched case-insensitively on the server
  action?: string;
  result?: string;
  reasonCode?: string;
  category?: ActivityCategory;
  occurredAfter?: string; // ISO 8601 date-time
  occurredBefore?: string; // ISO 8601 date-time
  page?: number;
  pageSize?: number;
}

export async function fetchAuditRecords(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  opts?: AuditQueryFilters,
): Promise<FetchResult<AuditListResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const params = new URLSearchParams();
  if (opts?.eventType) params.set('eventType', opts.eventType);
  if (opts?.entityType) params.set('entityType', opts.entityType);
  if (opts?.entityId) params.set('entityId', opts.entityId);
  if (opts?.actorHash) params.set('actorHash', opts.actorHash);
  if (opts?.actorRef) params.set('actorRef', opts.actorRef);
  if (opts?.action) params.set('action', opts.action);
  if (opts?.result) params.set('result', opts.result);
  if (opts?.reasonCode) params.set('reasonCode', opts.reasonCode);
  if (opts?.category) params.set('category', opts.category);
  if (opts?.occurredAfter) params.set('occurredAfter', opts.occurredAfter);
  if (opts?.occurredBefore) params.set('occurredBefore', opts.occurredBefore);
  if (opts?.page) params.set('page', String(opts.page));
  if (opts?.pageSize) params.set('pageSize', String(opts.pageSize));
  const query = params.toString();
  try {
    const res = await fetch(`${apiBaseUrl}/audit${query ? `?${query}` : ''}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /audit returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as AuditListResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function erasePiiMapping(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  userId: string,
): Promise<FetchResult<Record<string, never>>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/audit/pii-mappings/${encodeURIComponent(userId)}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${bearerToken}` },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.ok) return { kind: 'ok', data: {} };
    return { kind: 'error', status: res.status, message: `DELETE /audit/pii-mappings returned ${res.status}` };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
