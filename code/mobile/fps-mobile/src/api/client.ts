// Type-only imports from the generated API client. The mobile shell never copies
// DTOs by hand and never sends spoofable tenant/requestor identifiers.
import type { paths as IdentityPaths } from '@fps/api-client/identity';

export type MeResponse =
  IdentityPaths['/me']['get']['responses']['200']['content']['application/json'];

export type SessionResult =
  | { kind: 'ok'; me: MeResponse }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export type ApiClientConfig = {
  apiBaseUrl: string;
  bearerToken: string;
};

export async function fetchMe({
  apiBaseUrl,
  bearerToken,
}: ApiClientConfig): Promise<SessionResult> {
  if (!apiBaseUrl || !bearerToken) {
    return { kind: 'unauthenticated' };
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/me`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${bearerToken}`,
        Accept: 'application/json',
      },
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : 'network error';
    return { kind: 'unreachable', message };
  }

  if (response.status === 401 || response.status === 403) {
    return { kind: 'unauthenticated' };
  }

  if (!response.ok) {
    return {
      kind: 'error',
      status: response.status,
      message: `Identity /me returned HTTP ${response.status}`,
    };
  }

  try {
    const me = (await response.json()) as MeResponse;
    return { kind: 'ok', me };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'invalid JSON';
    return { kind: 'error', status: response.status, message };
  }
}
