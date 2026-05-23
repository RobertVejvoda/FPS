import type { ApiClientConfig } from './client';

export type HrImportRowStatus = 'Created' | 'Updated' | 'Unchanged' | 'Rejected';

export type HrImportRow = {
  lineNumber: number;
  externalSubject: string;
  status: HrImportRowStatus;
  reason: string | null;
};

export type HrImportPreview = {
  rows: HrImportRow[];
  created: number;
  updated: number;
  unchanged: number;
  rejected: number;
};

export type HrImportCommitResult = {
  applied: number;
  rejected: number;
  errors: string[];
};

export type HrImportResult<T> =
  | { kind: 'ok'; data: T }
  | { kind: 'error'; message: string }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string };

async function postImport<T>(
  url: string,
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  file: File,
): Promise<HrImportResult<T>> {
  const form = new FormData();
  form.append('employees', file);
  try {
    const res = await fetch(`${apiBaseUrl}${url}`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearerToken}` },
      body: form,
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    const body = (await res.json()) as Record<string, unknown>;
    if (!res.ok) return { kind: 'error', message: (body['error'] as string) ?? `HTTP ${res.status}` };
    return { kind: 'ok', data: body as T };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export function previewHrImport(
  config: ApiClientConfig,
  file: File,
): Promise<HrImportResult<HrImportPreview>> {
  return postImport('/profile/admin/hr-import/preview', config, file);
}

export function commitHrImport(
  config: ApiClientConfig,
  file: File,
): Promise<HrImportResult<HrImportCommitResult>> {
  return postImport('/profile/admin/hr-import/commit', config, file);
}
