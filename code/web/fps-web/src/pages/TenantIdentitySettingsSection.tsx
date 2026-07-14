import { useCallback, useEffect, useState } from 'react';
import type { ApiClientConfig } from '../api/client';
import {
  fetchTenantIdentityConfig,
  saveTenantIdentityConfig,
  type TenantIdentityConfigResponse,
} from '../api/customer';
import {
  formatRoleClaimNames,
  formatRoleMapping,
  normalizeIdpBrokerAlias,
  parseRoleClaimNames,
  parseRoleMapping,
} from './identityConfigForm';

// AUTH012 (#795) — tenant-admin identity settings. Lets a tenant admin view and edit
// the identity configuration (issuer, audience, claim names, role mapping, local
// account policy, and the non-secret AUTH011 broker alias) that previously required
// manual API calls. A missing config is a normal state: it renders as an empty
// setup form, not an error.

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ready'; existing: TenantIdentityConfigResponse | null }
  | { kind: 'error'; message: string };

type SaveState =
  | { kind: 'idle' }
  | { kind: 'saving' }
  | { kind: 'saved' }
  | { kind: 'invalid'; message: string }
  | { kind: 'error'; message: string };

interface FormValues {
  trustedIssuer: string;
  audience: string;
  tenantClaimName: string;
  subjectClaimName: string;
  roleClaimNamesText: string;
  roleMappingText: string;
  localAccountPolicyEnabled: boolean;
  idpBrokerAliasText: string;
}

const EMPTY_FORM: FormValues = {
  trustedIssuer: '',
  audience: '',
  tenantClaimName: 'tenant_id',
  subjectClaimName: 'sub',
  roleClaimNamesText: '',
  roleMappingText: '',
  localAccountPolicyEnabled: false,
  idpBrokerAliasText: '',
};

function toForm(config: TenantIdentityConfigResponse): FormValues {
  return {
    trustedIssuer: config.trustedIssuer,
    audience: config.audience,
    tenantClaimName: config.tenantClaimName,
    subjectClaimName: config.subjectClaimName,
    roleClaimNamesText: formatRoleClaimNames(config.roleClaimNames),
    roleMappingText: formatRoleMapping(config.roleMapping),
    localAccountPolicyEnabled: config.localAccountPolicyEnabled,
    idpBrokerAliasText: config.idpBrokerAlias ?? '',
  };
}

export function TenantIdentitySettingsSection({
  cfg,
  tenantId,
  onSaved,
  onUnauthenticated,
}: {
  cfg: ApiClientConfig;
  tenantId: string;
  // Called after a successful save so the page can refresh readiness state.
  onSaved: () => void;
  onUnauthenticated: () => void;
}) {
  const [loadState, setLoadState] = useState<LoadState>({ kind: 'loading' });
  const [form, setForm] = useState<FormValues>(EMPTY_FORM);
  const [saveState, setSaveState] = useState<SaveState>({ kind: 'idle' });

  const load = useCallback(() => {
    setLoadState({ kind: 'loading' });
    setSaveState({ kind: 'idle' });
    fetchTenantIdentityConfig(cfg, tenantId).then(r => {
      if (r.kind === 'unauthenticated') { onUnauthenticated(); return; }
      if (r.kind === 'ok') {
        setForm(toForm(r.data));
        setLoadState({ kind: 'ready', existing: r.data });
      } else if (r.kind === 'notconfigured') {
        setForm(EMPTY_FORM);
        setLoadState({ kind: 'ready', existing: null });
      } else {
        setLoadState({ kind: 'error', message: r.message });
      }
    });
  }, [cfg.apiBaseUrl, cfg.bearerToken, tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => { load(); }, [load]);

  function set<K extends keyof FormValues>(key: K, value: FormValues[K]) {
    setForm(f => ({ ...f, [key]: value }));
    if (saveState.kind !== 'idle') setSaveState({ kind: 'idle' });
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();

    const mapping = parseRoleMapping(form.roleMappingText);
    if (!mapping.ok) {
      setSaveState({ kind: 'invalid', message: mapping.error });
      return;
    }

    setSaveState({ kind: 'saving' });
    const result = await saveTenantIdentityConfig(cfg, tenantId, {
      trustedIssuer: form.trustedIssuer.trim(),
      audience: form.audience.trim(),
      tenantClaimName: form.tenantClaimName.trim(),
      subjectClaimName: form.subjectClaimName.trim(),
      roleClaimNames: parseRoleClaimNames(form.roleClaimNamesText),
      roleMapping: mapping.value,
      localAccountPolicyEnabled: form.localAccountPolicyEnabled,
      idpBrokerAlias: normalizeIdpBrokerAlias(form.idpBrokerAliasText),
    });

    if (result.kind === 'unauthenticated') { onUnauthenticated(); return; }
    if (result.kind === 'ok') {
      setSaveState({ kind: 'saved' });
      load();
      onSaved();
    } else if (result.kind === 'invalid') {
      setSaveState({ kind: 'invalid', message: result.message });
    } else {
      setSaveState({ kind: 'error', message: result.message });
    }
  }

  return (
    <section style={card}>
      <h3 style={cardTitle}>Identity & Login Settings</h3>
      <p style={{ margin: '0 0 12px', fontSize: 12, color: '#6b7280' }}>
        How your employees sign in. Company SSO settings come from your identity provider;
        the sign-in screen passes the entered email as a prefill hint (<code>login_hint</code>).
        Nothing here grants access by itself — access always comes from validated sign-in tokens.
      </p>

      {loadState.kind === 'loading' && <p style={muted}>Loading…</p>}
      {loadState.kind === 'error' && (
        <div>
          <p style={{ color: '#b91c1c', fontSize: 13 }}>{loadState.message}</p>
          <button type="button" onClick={load} style={btnSm}>Retry</button>
        </div>
      )}

      {loadState.kind === 'ready' && (
        <form onSubmit={(e) => { void handleSave(e); }} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {loadState.existing === null && (
            <p style={{ margin: 0, fontSize: 13, color: '#92400e', background: '#fffbeb', border: '1px solid #fcd34d', borderRadius: 6, padding: '8px 12px' }}>
              Identity is not configured yet. Fill in the settings below to enable employee sign-in for this tenant.
            </p>
          )}

          <Field label="Trusted issuer" hint="The token issuer URL your identity provider uses (from your IdP or FairSpot operator).">
            <input value={form.trustedIssuer} onChange={e => set('trustedIssuer', e.target.value)}
              placeholder="https://auth.example.com/realms/fairspot" style={input} />
          </Field>

          <Field label="Audience" hint="The API audience expected in sign-in tokens.">
            <input value={form.audience} onChange={e => set('audience', e.target.value)}
              placeholder="fairspot-api" style={input} />
          </Field>

          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Field label="Tenant claim name" hint="Token claim carrying the tenant." grow>
              <input value={form.tenantClaimName} onChange={e => set('tenantClaimName', e.target.value)}
                placeholder="tenant_id" style={input} />
            </Field>
            <Field label="Subject claim name" hint="Token claim carrying the stable user id." grow>
              <input value={form.subjectClaimName} onChange={e => set('subjectClaimName', e.target.value)}
                placeholder="sub" style={input} />
            </Field>
          </div>

          <Field label="Role claim names" hint="Comma-separated token claims to read groups/roles from (e.g. groups, roles).">
            <input value={form.roleClaimNamesText} onChange={e => set('roleClaimNamesText', e.target.value)}
              placeholder="groups" style={input} />
          </Field>

          <Field label="Role mapping" hint={'One mapping per line: "idp-group = fairspot-role". Valid FairSpot roles: employee, admin, hr_manager, report_viewer, auditor. Unmapped groups are ignored.'}>
            <textarea value={form.roleMappingText} onChange={e => set('roleMappingText', e.target.value)}
              placeholder={'fairspot-admins = admin\nall-employees = employee'}
              rows={4} style={{ ...input, resize: 'vertical', fontFamily: 'monospace', fontSize: 12 }} />
          </Field>

          <Field
            label="SSO broker alias (optional)"
            hint={'Non-secret routing hint for company SSO: the Keycloak identity-provider broker alias for your company IdP. When set (and your login mode is Company SSO), the sign-in screen sends it as kc_idp_hint so employees skip the account chooser and go straight to your IdP. This is routing metadata only — never a client secret, and it does not grant access. Leave empty if no external IdP broker is configured.'}
          >
            <input value={form.idpBrokerAliasText} onChange={e => set('idpBrokerAliasText', e.target.value)}
              placeholder="acme-entra" autoComplete="off" style={input} />
          </Field>

          <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 13, color: '#111827' }}>
            <input type="checkbox" checked={form.localAccountPolicyEnabled}
              onChange={e => set('localAccountPolicyEnabled', e.target.checked)} style={{ marginTop: 2 }} />
            <span>
              <strong>Allow FairSpot-local accounts</strong>
              <span style={{ display: 'block', fontSize: 12, color: '#6b7280' }}>
                Permits local fallback and break-glass accounts for this tenant. Tenants using the
                combined login mode keep the standard sign-in screen, so local accounts stay reachable.
              </span>
            </span>
          </label>

          {saveState.kind === 'invalid' && (
            <p style={{ margin: 0, color: '#b91c1c', fontSize: 13 }}>{saveState.message}</p>
          )}
          {saveState.kind === 'error' && (
            <p style={{ margin: 0, color: '#b91c1c', fontSize: 13 }}>
              Saving failed: {saveState.message} Try again.
            </p>
          )}
          {saveState.kind === 'saved' && (
            <p style={{ margin: 0, color: '#166534', fontSize: 13 }}>
              Identity settings saved. Readiness is being refreshed.
            </p>
          )}

          <div style={{ display: 'flex', gap: 8 }}>
            <button type="submit" disabled={saveState.kind === 'saving'} style={btn}>
              {saveState.kind === 'saving' ? 'Saving…' : 'Save identity settings'}
            </button>
            <button type="button" onClick={load} disabled={saveState.kind === 'saving'} style={btnSecondary}>
              Discard changes
            </button>
          </div>
        </form>
      )}
    </section>
  );
}

function Field({ label, hint, grow, children }: { label: string; hint?: string; grow?: boolean; children: React.ReactNode }) {
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 13, fontWeight: 600, color: '#111827', ...(grow ? { flex: 1, minWidth: 220 } : {}) }}>
      {label}
      {children}
      {hint ? <span style={{ fontSize: 12, fontWeight: 400, color: '#6b7280' }}>{hint}</span> : null}
    </label>
  );
}

const card: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '16px 20px' };
const cardTitle: React.CSSProperties = { margin: '0 0 12px', fontSize: 15, fontWeight: 700 };
const muted: React.CSSProperties = { color: '#6b7280', fontSize: 13 };
const input: React.CSSProperties = { border: '1px solid #e5e7eb', borderRadius: 6, padding: '8px 12px', fontSize: 14, color: '#111827', background: '#fff', width: '100%', boxSizing: 'border-box', fontWeight: 400 };
const btn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
const btnSecondary: React.CSSProperties = { background: '#fff', color: '#374151', border: '1px solid #e5e7eb', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
const btnSm: React.CSSProperties = { ...btn, padding: '6px 12px', fontSize: 13 };
