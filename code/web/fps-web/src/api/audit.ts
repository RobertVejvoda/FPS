import type { ApiClientConfig, FetchResult } from './client';

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
}

export interface AuditListResponse {
  items: AuditRecord[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export async function fetchAuditRecords(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  opts?: { eventType?: string; entityType?: string; page?: number },
): Promise<FetchResult<AuditListResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  const params = new URLSearchParams();
  if (opts?.eventType) params.set('eventType', opts.eventType);
  if (opts?.entityType) params.set('entityType', opts.entityType);
  if (opts?.page) params.set('page', String(opts.page));
  const query = params.toString();
  try {
    const res = await fetch(`${apiBaseUrl}/audit${query ? `?${query}` : ''}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
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
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.ok) return { kind: 'ok', data: {} };
    return { kind: 'error', status: res.status, message: `DELETE /audit/pii-mappings returned ${res.status}` };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
