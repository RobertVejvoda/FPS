import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

const phaseMessages: Record<string, string> = {
  'login-cancelled': 'Sign in was cancelled. Try again.',
  'login-failed': 'Sign in failed. Try again.',
  'session-expired': 'Your session has expired. Please sign in again.',
  'unreachable': 'Cannot reach the backend. Check your connection and try again.',
};

export function SessionPage() {
  const { phase, phaseError, apiBaseUrl, devFallbackEnabled, branding, login, save, clear } = useAuth();
  const navigate = useNavigate();

  const [showDevForm, setShowDevForm] = useState(false);
  const [urlInput, setUrlInput] = useState(apiBaseUrl);
  const [tokenInput, setTokenInput] = useState('');
  const [formError, setFormError] = useState('');
  const [saving, setSaving] = useState(false);

  // Navigate away once login completes successfully.
  useEffect(() => {
    if (phase === 'authenticated') {
      navigate('/bookings', { replace: true });
    }
  }, [phase, navigate]);

  if (phase === 'loading' || phase === 'validating') {
    return (
      <div className="session-shell">
        <div className="session-story">
          <BrandLockup branding={branding} />
          <div>
            <h1>Fair allocation, visible outcomes.</h1>
            <p>Request workplace resources, understand the result, and keep operational decisions traceable.</p>
          </div>
          <DemoPills />
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
            <h1>Configuration needs attention.</h1>
            <p>The app cannot load the runtime identity settings required for sign-in.</p>
          </div>
          <DemoPills />
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
          <h1>Parking today, handled fairly.</h1>
          <p>Employees see their own status. Operators see readiness, policy, reporting, and audit evidence.</p>
        </div>
        <DemoPills />
      </div>

      <div className="session-panel-wrap">
        <div className="session-panel">
          <BrandLockup branding={branding} compact />
          <h2>Sign in</h2>
          <p>
            {branding.tenantName
              ? `Continue to ${branding.tenantName}.`
              : 'Continue to the employee portal.'}
          </p>

          {statusMessage ? (
            <p style={{ marginTop: 14, color: 'var(--danger)', fontSize: 13 }}>{statusMessage}</p>
          ) : null}

          <button
            onClick={() => { void login(); }}
            className="btn-primary"
            style={{ width: '100%', marginTop: 22 }}
          >
            Sign in with SSO
          </button>

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

function DemoPills() {
  return (
    <div className="demo-pills" aria-label="Demo highlights">
      <span className="demo-pill">Fair allocation</span>
      <span className="demo-pill">Tenant ready</span>
      <span className="demo-pill">Audit evidence</span>
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
