// AUTH012 (#795) — pure parsing/formatting helpers for the tenant-admin identity
// settings form. Kept free of React/fetch so they can be unit-tested directly.

// Role claim names are edited as a comma-separated line ("groups, roles").
export function parseRoleClaimNames(text: string): string[] {
  return text
    .split(',')
    .map(s => s.trim())
    .filter(s => s.length > 0);
}

export function formatRoleClaimNames(names: string[]): string {
  return names.join(', ');
}

export type RoleMappingParse =
  | { ok: true; value: Record<string, string> }
  | { ok: false; error: string };

// Role mapping is edited one pair per line as "idp-group = fairspot-role".
// Blank lines are ignored. Duplicate IdP group names are an error because the
// server would silently keep only one of them.
export function parseRoleMapping(text: string): RoleMappingParse {
  const value: Record<string, string> = {};
  const lines = text.split('\n');
  for (const rawLine of lines) {
    const line = rawLine.trim();
    if (line.length === 0) continue;
    const eq = line.indexOf('=');
    if (eq <= 0 || eq === line.length - 1) {
      return { ok: false, error: `Each mapping line must look like "idp-group = fairspot-role" (problem line: "${line}").` };
    }
    const key = line.slice(0, eq).trim();
    const role = line.slice(eq + 1).trim();
    if (!key || !role) {
      return { ok: false, error: `Each mapping line must look like "idp-group = fairspot-role" (problem line: "${line}").` };
    }
    if (Object.prototype.hasOwnProperty.call(value, key)) {
      return { ok: false, error: `The group "${key}" is mapped more than once. Keep one line per group.` };
    }
    value[key] = role;
  }
  return { ok: true, value };
}

export function formatRoleMapping(mapping: Record<string, string>): string {
  return Object.entries(mapping)
    .map(([group, role]) => `${group} = ${role}`)
    .join('\n');
}

// The broker alias field treats whitespace-only input as "not configured" — the
// server does the same normalisation, so the form never sends an empty string.
export function normalizeIdpBrokerAlias(input: string): string | null {
  const trimmed = input.trim();
  return trimmed.length === 0 ? null : trimmed;
}

// Required-field guard run before submit. Blank claim names are dangerous, not just
// invalid: a persisted empty tenant claim makes sign-in fail closed for every tenant
// user while readiness still passes, so the form must never send one (PR #796).
export function validateRequiredIdentityFields(fields: {
  trustedIssuer: string;
  audience: string;
  tenantClaimName: string;
  subjectClaimName: string;
}): string | null {
  if (fields.trustedIssuer.trim().length === 0) return 'Trusted issuer is required.';
  if (fields.audience.trim().length === 0) return 'Audience is required.';
  if (fields.tenantClaimName.trim().length === 0) return 'Tenant claim name is required. Use "tenant_id" unless your identity provider issues a different claim.';
  if (fields.subjectClaimName.trim().length === 0) return 'Subject claim name is required. Use "sub" unless your identity provider issues a different claim.';
  return null;
}
