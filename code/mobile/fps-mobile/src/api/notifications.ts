import type { ApiClientConfig } from './client';

export type NotificationItem = {
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
};

export type NotificationsResult =
  | { kind: 'ok'; items: NotificationItem[]; hasMore: boolean }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type UnreadCountResult =
  | { kind: 'ok'; count: number }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type MarkReadResult =
  | { kind: 'ok' }
  | { kind: 'notFound' }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchNotifications(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  opts?: { unreadOnly?: boolean; type?: string; pageSize?: number },
): Promise<NotificationsResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };

  const params = new URLSearchParams();
  if (opts?.unreadOnly) params.set('unreadOnly', 'true');
  if (opts?.type) params.set('type', opts.type);
  if (opts?.pageSize != null) params.set('pageSize', String(opts.pageSize));
  const query = params.toString();
  const url = `${apiBaseUrl}/notifications${query ? `?${query}` : ''}`;

  let response: Response;
  try {
    response = await fetch(url, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
  } catch (error) {
    return { kind: 'unreachable', message: error instanceof Error ? error.message : 'network error' };
  }

  if (response.status === 401 || response.status === 403) return { kind: 'unauthenticated' };
  if (!response.ok) return { kind: 'error', status: response.status, message: `Notification API returned HTTP ${response.status}` };

  try {
    const data = (await response.json()) as { items: NotificationItem[]; hasMore: boolean };
    return { kind: 'ok', items: data.items, hasMore: data.hasMore };
  } catch {
    return { kind: 'error', status: response.status, message: 'Invalid response body.' };
  }
}

export async function fetchUnreadCount(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<UnreadCountResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/notifications/unread-count`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
  } catch (error) {
    return { kind: 'unreachable', message: error instanceof Error ? error.message : 'network error' };
  }

  if (response.status === 401 || response.status === 403) return { kind: 'unauthenticated' };
  if (!response.ok) return { kind: 'error', status: response.status, message: `Unread count returned HTTP ${response.status}` };

  try {
    const data = (await response.json()) as { count: number };
    return { kind: 'ok', count: data.count };
  } catch {
    return { kind: 'error', status: response.status, message: 'Invalid response body.' };
  }
}

export async function markNotificationRead(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  notificationId: string,
): Promise<MarkReadResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/notifications/${encodeURIComponent(notificationId)}/read`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearerToken}` },
    });
  } catch (error) {
    return { kind: 'unreachable', message: error instanceof Error ? error.message : 'network error' };
  }

  if (response.status === 401 || response.status === 403) return { kind: 'unauthenticated' };
  if (response.status === 204) return { kind: 'ok' };
  if (response.status === 404) return { kind: 'notFound' };
  return { kind: 'error', status: response.status, message: `Mark read returned HTTP ${response.status}` };
}
