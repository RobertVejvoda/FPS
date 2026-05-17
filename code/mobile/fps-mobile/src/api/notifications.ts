import type { components } from '@fps/api-client/notification';
import type { ApiClientConfig } from './client';

export type NotificationDto = components['schemas']['NotificationDto'];
export type NotificationListResponse = components['schemas']['NotificationListResponse'];
export type UnreadCountResponse = components['schemas']['UnreadCountResponse'];

export type NotificationsResult =
  | { kind: 'ok'; items: NotificationDto[]; hasMore: boolean }
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
  config: ApiClientConfig,
  opts?: {
    unreadOnly?: boolean;
    type?: string;
    pageSize?: number;
  }
): Promise<NotificationsResult> {
  const params = new URLSearchParams();
  if (opts?.unreadOnly !== undefined) params.set('unreadOnly', String(opts.unreadOnly));
  if (opts?.type) params.set('type', opts.type);
  if (opts?.pageSize) params.set('pageSize', String(opts.pageSize));

  const query = params.toString();
  const url = `${config.apiBaseUrl}/notifications${query ? `?${query}` : ''}`;

  try {
    const res = await fetch(url, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${config.bearerToken}`,
        'Content-Type': 'application/json'
      }
    });

    if (res.status === 401) return { kind: 'unauthenticated' };

    if (!res.ok) {
      const text = await res.text().catch(() => '');
      return { kind: 'error', status: res.status, message: text || `HTTP ${res.status}` };
    }

    const data: NotificationListResponse = await res.json();
    return {
      kind: 'ok',
      items: data.items,
      hasMore: data.hasMore
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Unknown error';
    return { kind: 'unreachable', message };
  }
}

export async function fetchUnreadCount(
  config: ApiClientConfig
): Promise<UnreadCountResult> {
  const url = `${config.apiBaseUrl}/notifications/unread-count`;

  try {
    const res = await fetch(url, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${config.bearerToken}`,
        'Content-Type': 'application/json'
      }
    });

    if (res.status === 401) return { kind: 'unauthenticated' };

    if (!res.ok) {
      const text = await res.text().catch(() => '');
      return { kind: 'error', status: res.status, message: text || `HTTP ${res.status}` };
    }

    const data: UnreadCountResponse = await res.json();
    return {
      kind: 'ok',
      count: typeof data.count === 'string' ? parseInt(data.count, 10) : data.count
    };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Unknown error';
    return { kind: 'unreachable', message };
  }
}

export async function markNotificationRead(
  config: ApiClientConfig,
  notificationId: string
): Promise<MarkReadResult> {
  const url = `${config.apiBaseUrl}/notifications/${notificationId}/read`;

  try {
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${config.bearerToken}`,
        'Content-Type': 'application/json'
      }
    });

    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 404) return { kind: 'notFound' };
    if (res.status === 204) return { kind: 'ok' };

    if (!res.ok) {
      const text = await res.text().catch(() => '');
      return { kind: 'error', status: res.status, message: text || `HTTP ${res.status}` };
    }

    return { kind: 'ok' };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Unknown error';
    return { kind: 'unreachable', message };
  }
}
