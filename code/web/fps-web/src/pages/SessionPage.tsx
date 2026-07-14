import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { defaultRoute } from '../auth/roles';
import { discoverTenant } from '../api/customer';
import { useEffect } from 'react';
import { t, useLocale, type MessageKey } from '../i18n';
import { LocaleSwitcher } from '../components/LocaleSwitcher';

const phaseMessageKeys: Record<string, MessageKey> = {
  'login-cancelled': 'session.phase.loginCancelled',
  'login-failed': 'session.phase.loginFailed',
  'session-expired': 'session.phase.sessionExpired',
  'unreachable': 'session.phase.unreachable',
};

export function SessionPage() {
  const { phase, phaseError, apiBaseUrl, devFallbackEnabled, branding, roles, login, save, clear } = useAuth();
  const navigate = useNavigate();

  const [showDevForm, setShowDevForm] = useState(false);
  const [urlInput, setUrlInput] = useState(apiBaseUrl);
  const [tokenInput, setTokenInput] = useState('');
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (phase === 'authenticated') {
      navigate(defaultRoute(roles), { replace: true });
    }
  }, [phase, roles, navigate]);

  if (phase === 'loading' || phase === 'validating') {
    return (
      <div className="session-shell">
        <div className="session-story">
          <BrandLockup branding={branding} />
          <div>
            <p className="session-eyebrow">{t('session.eyebrow.workplace')}</p>
            <h1>{t('session.hero.title1')}</h1>
            <p>{t('session.hero.body1')}</p>
          </div>
          <SessionSnapshot />
        </div>
        <div className="session-panel-wrap">
          <div className="session-panel">
            <p>
              {phase === 'validating' ? t('session.verifying') : t('common.loading')}
            </p>
          </div>
        </div>
      </div>
    );
  }

  if (phase === 'invalid-config') {
    return (
      <div className="session-shell">
        <div className="session-story">
          <BrandLockup branding={branding} />
          <div>
            <p className="session-eyebrow">{t('session.eyebrow.config')}</p>
            <h1>{t('session.config.title')}</h1>
            <p>{t('session.config.body')}</p>
          </div>
          <SessionSnapshot />
        </div>
        <div className="session-panel-wrap">
          <div className="session-panel">
            <h2>{t('session.config.errorHeading')}</h2>
            <p>{phaseError ?? t('session.config.errorFallback')}</p>
            <p style={{ marginTop: 12 }}>
              {t('session.config.hintPrefix')} <code>/config.json</code> {t('session.config.hintSuffix')}
            </p>
          </div>
        </div>
      </div>
    );
  }

  const statusMessageKey = phaseMessageKeys[phase];

  async function handleDevSave(e: React.FormEvent) {
    e.preventDefault();
    if (!urlInput.trim() || !tokenInput.trim()) {
      setFormError(t('session.dev.bothRequired'));
      return;
    }
    setFormError('');
    setSaving(true);
    await save(urlInput, tokenInput);
    setSaving(false);
  }

  return (
    <div className="session-shell">
      <div className="session-story">
        <BrandLockup branding={branding} />
        <div>
          <p className="session-eyebrow">{t('session.eyebrow.brand')}</p>
          {/* UX008 (#781): module-neutral positioning — FairSpot allocates parking, seats, and future modules. */}
          <h1>{t('session.hero.title2')}</h1>
          <p>{t('session.hero.body2')}</p>
        </div>
        <SessionSnapshot />
        <Link className="session-legal-link" to="/legal">{t('session.legalLink')}</Link>
      </div>

      <div className="session-panel-wrap">
        <div className="session-panel">
          <BrandLockup branding={branding} compact />
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
            <h2 style={{ margin: 0 }}>{t('session.signIn.heading')}</h2>
            <LocaleSwitcher />
          </div>
          <p>
            {branding.tenantName
              ? t('session.signIn.secureAccessFor', { tenantName: branding.tenantName })
              : t('session.signIn.secureAccessDefault')}
          </p>

          {statusMessageKey ? (
            <p style={{ marginTop: 14, color: 'var(--danger)', fontSize: 13 }}>{t(statusMessageKey)}</p>
          ) : null}

          <EmailFirstSignIn apiBaseUrl={apiBaseUrl} onLogin={login} />

          <div className="session-security-note">
            <span>{t('session.security.fairAllocation')}</span>
            <span>{t('session.security.teamPolicies')}</span>
            <span>{t('session.security.clearOutcomes')}</span>
          </div>

          {devFallbackEnabled ? (
            <div style={{ marginTop: 18 }}>
              <button
                type="button"
                onClick={() => { setShowDevForm(v => !v); }}
                className="btn-ghost"
                style={{ minHeight: 0, padding: 0, textDecoration: 'underline' }}
              >
                {showDevForm ? t('session.dev.hide') : t('session.dev.show')}
              </button>

              {showDevForm ? (
                <form onSubmit={(e) => { void handleDevSave(e); }} style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 14 }}>
                  <p>
                    {t('session.dev.note')}
                  </p>
                  <label style={labelStyle}>
                    {t('session.dev.apiBaseUrl')}
                    <input
                      value={urlInput}
                      onChange={(e) => setUrlInput(e.target.value)}
                      placeholder="http://localhost:10000"
                      autoComplete="off"
                      style={inputStyle}
                    />
                  </label>
                  <label style={labelStyle}>
                    {t('session.dev.bearerToken')}
                    <textarea
                      value={tokenInput}
                      onChange={(e) => setTokenInput(e.target.value)}
                      placeholder="eyJhbGciOiJI..."
                      rows={4}
                      style={{ ...inputStyle, resize: 'vertical', fontFamily: 'monospace', fontSize: 12 }}
                    />
                  </label>
                  {formError ? <p style={{ color: 'var(--danger)', fontSize: 13 }}>{formError}</p> : null}
                  <button type="submit" disabled={saving} className="btn-primary">
                    {saving ? t('session.dev.verifying') : t('session.dev.useToken')}
                  </button>
                  <button type="button" onClick={clear} className="btn-secondary">
                    {t('session.dev.clearToken')}
                  </button>
                </form>
              ) : null}
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}

type SignInStep = 'idle' | 'discovering' | 'routing-sso' | 'routing-local' | 'notfound' | 'error';

// AUTH010 (#788) — one email-first entry point. The user types their email; FairSpot
// discovers the tenant's login route from the domain and continues automatically:
// company SSO for CompanySso tenants, the FairSpot sign-in for LocalAccount/Both
// tenants. Discovery is routing only — access still comes from validated token
// claims and /me after authentication. Unknown domains get an opaque state with a
// FairSpot-account fallback so responses never confirm which tenants exist.
function EmailFirstSignIn({
  apiBaseUrl,
  onLogin,
}: {
  apiBaseUrl: string;
  onLogin: (loginHint?: string, opts?: { idpHint?: string }) => Promise<void>;
}) {
  const [email, setEmail] = useState('');
  const [step, setStep] = useState<SignInStep>('idle');
  const { applyTenantDefault } = useLocale();

  const trimmed = email.trim();
  const atIndex = trimmed.indexOf('@');
  const domain = atIndex > 0 ? trimmed.slice(atIndex + 1).trim() : '';
  const routing = step === 'routing-sso' || step === 'routing-local';
  const canSubmit = domain.length > 1 && step !== 'discovering' && !routing;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit) return;
    setStep('discovering');
    const result = await discoverTenant(apiBaseUrl, domain);
    if (result.kind === 'ok') {
      // Route automatically by the tenant's configured login mode. The email is
      // passed only as an OIDC login_hint; it is never stored. AUTH011 (#793):
      // discovery returns a non-secret broker alias only for CompanySso tenants —
      // when present it is sent as kc_idp_hint so Keycloak skips the account
      // chooser and goes straight to the company IdP. Both/LocalAccount tenants
      // never get an alias, so the Keycloak chooser (and local fallback) stays
      // reachable for them.
      const sso = result.data.loginMode === 'CompanySso';
      setStep(sso ? 'routing-sso' : 'routing-local');
      // LOC001 review (#802): apply the discovered tenant default locale before
      // redirecting so Keycloak's ui_locales matches the tenant language even
      // for a first-time visitor with an English browser. Transient tenant
      // default only — a stored user language choice still wins.
      applyTenantDefault(result.data.defaultLocale);
      await onLogin(trimmed, sso && result.data.idpAlias ? { idpHint: result.data.idpAlias } : undefined);
    } else if (result.kind === 'notfound') {
      setStep('notfound');
    } else {
      setStep('error');
    }
  }

  function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    setEmail(e.target.value);
    if (step !== 'idle') setStep('idle');
  }

  return (
    <form onSubmit={(e) => { void handleSubmit(e); }} style={{ marginTop: 22 }}>
      <label style={labelStyle}>
        {t('session.email.label')}
        <input
          type="email"
          value={email}
          onChange={handleChange}
          placeholder="you@yourcompany.com"
          autoComplete="email"
          autoCapitalize="none"
          style={inputStyle}
        />
      </label>
      {step === 'notfound' ? (
        <p style={discoveryMessageStyle}>
          {t('session.email.notFound')}
        </p>
      ) : step === 'error' ? (
        <p style={discoveryMessageStyle}>
          {t('session.email.error')}
        </p>
      ) : null}
      <button
        type="submit"
        disabled={!canSubmit}
        className="btn-primary"
        style={{ width: '100%', marginTop: 10, minHeight: 46 }}
      >
        {step === 'discovering' ? t('session.email.finding')
          : step === 'routing-sso' ? t('session.email.routingSso')
          : step === 'routing-local' ? t('session.email.routingLocal')
          : t('session.email.continue')}
      </button>
      {/* The FairSpot-local path stays reachable without discovery — demo users,
          small tenants, fallback, and break-glass accounts. A quiet secondary link,
          not an equal first choice. */}
      <button
        type="button"
        onClick={() => { void onLogin(trimmed || undefined); }}
        className="btn-ghost"
        style={{ width: '100%', marginTop: 10, minHeight: 0, textDecoration: 'underline', fontSize: 13 }}
      >
        {t('session.email.signInFairspot')}
      </button>
    </form>
  );
}

function BrandLockup({ branding, compact = false }: { branding: { productName: string; tenantName: string; logoUrl: string }; compact?: boolean }) {
  return (
    <div className="brand-lockup" style={compact ? { minWidth: 0 } : undefined}>
      <div className="brand-mark" aria-hidden="true">
        {branding.logoUrl ? <img src={branding.logoUrl} alt="" /> : branding.productName.slice(0, 1)}
      </div>
      <div className="brand-title">
        <strong>{branding.productName}</strong>
        {branding.tenantName ? <span>{branding.tenantName}</span> : null}
      </div>
    </div>
  );
}

function SessionSnapshot() {
  return (
    <div className="session-snapshot" aria-label={t('session.snapshot.ariaLabel')}>
      <div className="session-snapshot-card session-snapshot-card-strong">
        <span>{t('session.snapshot.nextDraw')}</span>
        <strong>18:00</strong>
        <small>{t('session.snapshot.nextDrawSub')}</small>
      </div>
      <div className="session-snapshot-card">
        <span>{t('session.snapshot.hrView')}</span>
        <strong>{t('session.snapshot.hrViewValue')}</strong>
        <small>{t('session.snapshot.hrViewSub')}</small>
      </div>
      <div className="session-snapshot-card">
        <span>{t('session.snapshot.evidence')}</span>
        <strong>{t('session.snapshot.evidenceValue')}</strong>
        <small>{t('session.snapshot.evidenceSub')}</small>
      </div>
    </div>
  );
}

const labelStyle: React.CSSProperties = {
  display: 'flex',
  flexDirection: 'column',
  gap: 4,
  fontSize: 13,
  fontWeight: 600,
  color: '#111827',
};

const inputStyle: React.CSSProperties = {
  border: '1px solid #e5e7eb',
  borderRadius: 6,
  padding: '8px 12px',
  fontSize: 14,
  color: '#111827',
  background: '#fff',
  width: '100%',
  boxSizing: 'border-box',
};

const discoveryMessageStyle: React.CSSProperties = {
  marginTop: 8,
  fontSize: 13,
  color: 'var(--muted)',
  lineHeight: 1.4,
};
