import type { paths as IdentityPaths } from '@fps/api-client/identity';

export type MeResponse =
  IdentityPaths['/me']['get']['responses']['200']['content']['application/json'];

export type ApiClientConfig = {
  apiBaseUrl: string;
  bearerToken: string;
};

export type FetchResult<T> =
  | { kind: 'ok'; data: T }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchMe({ apiBaseUrl, bearerToken }: ApiClientConfig): Promise<FetchResult<MeResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/me`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /me returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as MeResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
