import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { defaultRoute } from '../auth/roles';
import { discoverTenant } from '../api/customer';
import { useEffect } from 'react';

const phaseMessages: Record<string, string> = {
  'login-cancelled': 'Sign in was cancelled. Try again.',
  'login-failed': 'Sign in failed. Try again.',
  'session-expired': 'Your session has expired. Please sign in again.',
  'unreachable': 'Cannot reach the backend. Check your connection and try again.',
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
            <p className="session-eyebrow">Workplace parking operations</p>
            <h1>Fair allocation with evidence your business can trust.</h1>
            <p>Request shared spots and seats, run policy-based Draws, and give HR a clear operational record without exposing private employee data.</p>
          </div>
          <SessionSnapshot />
        </div>
        <div className="session-panel-wrap">
          <div className="session-panel">
            <p>
              {phase === 'validating' ? 'Verifying session…' : 'Loading…'}
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
            <p className="session-eyebrow">Runtime configuration</p>
            <h1>Configuration needs attention.</h1>
            <p>The app cannot load the runtime identity settings required for sign-in.</p>
          </div>
          <SessionSnapshot />
        </div>
        <div className="session-panel-wrap">
          <div className="session-panel">
            <h2>Configuration error</h2>
            <p>{phaseError ?? 'Unable to load /config.json.'}</p>
            <p style={{ marginTop: 12 }}>
              Ensure <code>/config.json</code> is served by the web server and contains valid OIDC settings.
            </p>
          </div>
        </div>
      </div>
    );
  }

  const statusMessage = phaseMessages[phase];

  async function handleDevSave(e: React.FormEvent) {
    e.preventDefault();
    if (!urlInput.trim() || !tokenInput.trim()) {
      setFormError('Both fields are required.');
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
          <p className="session-eyebrow">FairSpot for modern workplaces</p>
          {/* UX008 (#781): module-neutral positioning — FairSpot allocates parking, seats, and future modules. */}
          <h1>Fair allocation employees can understand.</h1>
          <p>Give employees a clear answer, give HR operational control, and keep every Draw ready for review.</p>
        </div>
        <SessionSnapshot />
        <Link className="session-legal-link" to="/legal">Legal notices</Link>
      </div>

      <div className="session-panel-wrap">
        <div className="session-panel">
          <BrandLockup branding={branding} compact />
          <h2>Sign in</h2>
          <p>
            {branding.tenantName
              ? `Secure access for ${branding.tenantName}.`
              : 'Secure access to employee and HR workspaces.'}
          </p>

          {statusMessage ? (
            <p style={{ marginTop: 14, color: 'var(--danger)', fontSize: 13 }}>{statusMessage}</p>
          ) : null}

          <CompanySsoPath apiBaseUrl={apiBaseUrl} onLogin={login} />

          <div style={dividerStyle}>
            <span style={dividerLabelStyle}>or</span>
          </div>

          <button
            onClick={() => { void login(); }}
            className="btn-secondary"
            style={{ width: '100%', minHeight: 46 }}
          >
            Sign in with FairSpot account
          </button>

          <div className="session-security-note">
            <span>Fair parking</span>
            <span>Team policies</span>
            <span>Clear outcomes</span>
          </div>

          {devFallbackEnabled ? (
            <div style={{ marginTop: 18 }}>
              <button
                type="button"
                onClick={() => { setShowDevForm(v => !v); }}
                className="btn-ghost"
                style={{ minHeight: 0, padding: 0, textDecoration: 'underline' }}
              >
                {showDevForm ? 'Hide development access' : 'Development access'}
              </button>

              {showDevForm ? (
                <form onSubmit={(e) => { void handleDevSave(e); }} style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 14 }}>
                  <p>
                    Development only. Paste the token from the local smoke script.
                  </p>
                  <label style={labelStyle}>
                    API base URL
                    <input
                      value={urlInput}
                      onChange={(e) => setUrlInput(e.target.value)}
                      placeholder="http://localhost:10000"
                      autoComplete="off"
                      style={inputStyle}
                    />
                  </label>
                  <label style={labelStyle}>
                    Bearer token
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
                    {saving ? 'Verifying…' : 'Use token'}
                  </button>
                  <button type="button" onClick={clear} className="btn-secondary">
                    Clear stored token
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

type SsoStep = 'idle' | 'discovering' | 'notfound' | 'error';

function CompanySsoPath({
  apiBaseUrl,
  onLogin,
}: {
  apiBaseUrl: string;
  onLogin: (loginHint?: string) => Promise<void>;
}) {
  const [email, setEmail] = useState('');
  const [step, setStep] = useState<SsoStep>('idle');

  const domain = email.includes('@') ? email.slice(email.indexOf('@') + 1).trim() : '';
  const canSubmit = domain.length > 1 && step !== 'discovering';

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit) return;
    setStep('discovering');
    const result = await discoverTenant(apiBaseUrl, domain);
    if (result.kind === 'ok') {
      await onLogin(email);
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
        Work email
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
          We couldn't find your company. Check your work email address or use a FairSpot account to sign in.
        </p>
      ) : step === 'error' ? (
        <p style={discoveryMessageStyle}>
          Something went wrong. Try again or use a FairSpot account to sign in.
        </p>
      ) : null}
      <button
        type="submit"
        disabled={!canSubmit}
        className="btn-primary"
        style={{ width: '100%', marginTop: 10, minHeight: 46 }}
      >
        {step === 'discovering' ? 'Looking up your company…' : 'Continue with company SSO'}
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
    <div className="session-snapshot" aria-label="Operational highlights">
      <div className="session-snapshot-card session-snapshot-card-strong">
        <span>Next Draw</span>
        <strong>18:00</strong>
        <small>Policy window visible to employees</small>
      </div>
      <div className="session-snapshot-card">
        <span>HR view</span>
        <strong>Live</strong>
        <small>Requests, outcomes, and exceptions</small>
      </div>
      <div className="session-snapshot-card">
        <span>Evidence</span>
        <strong>Traceable</strong>
        <small>Allocation decisions kept for review</small>
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

const dividerStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  gap: 10,
  margin: '18px 0',
  color: 'var(--muted)',
  fontSize: 12,
};

const dividerLabelStyle: React.CSSProperties = {
  flexShrink: 0,
  padding: '0 6px',
  background: '#fff',
};

const discoveryMessageStyle: React.CSSProperties = {
  marginTop: 8,
  fontSize: 13,
  color: 'var(--muted)',
  lineHeight: 1.4,
};
