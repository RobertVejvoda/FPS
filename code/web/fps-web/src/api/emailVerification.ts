import type { ApiClientConfig } from './client';

// AUTH008B (#734) — confirm an email-verification token. The token is the Secret from the emailed link;
// it is sent ONLY in the request body (never a query string on the API), matching the server contract.
export type ConfirmEmailResult =
  | { kind: 'verified' }
  | { kind: 'rejected'; reason: string }
  | { kind: 'unauthenticated' }
  | { kind: 'error' };

export async function confirmEmailVerification(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  token: string,
): Promise<ConfirmEmailResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/profile/email/verification/confirm`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${bearerToken}` },
      body: JSON.stringify({ token }),
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (res.ok) return { kind: 'verified' };
    if (res.status === 400) {
      const body = (await res.json().catch(() => null)) as { reason?: string } | null;
      return { kind: 'rejected', reason: body?.reason ?? 'verification_failed' };
    }
    return { kind: 'error' };
  } catch {
    return { kind: 'error' };
  }
}
