import type { ApiClientConfig, FetchResult } from './client';

export interface NotificationItem {
  id: string;
  notificationType: string;
  messageText: string;
  relatedRequestId: string | null;
  relatedDate: string | null;
  relatedTimeSlot: string | null;
  locationId: string | null;
  nextAction: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationListResponse {
  items: NotificationItem[];
  totalReturned: number;
  hasMore: boolean;
}

export async function fetchNotifications(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<NotificationListResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/notifications`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /notifications returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as NotificationListResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchUnreadCount(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<{ count: number }>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/notifications/unread-count`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /notifications/unread-count returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as { count: number } };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function markNotificationRead(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  notificationId: string,
): Promise<FetchResult<Record<string, never>>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/notifications/${encodeURIComponent(notificationId)}/read`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearerToken}` },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (res.ok) return { kind: 'ok', data: {} };
    return { kind: 'error', status: res.status, message: `POST /notifications/${notificationId}/read returned ${res.status}` };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
