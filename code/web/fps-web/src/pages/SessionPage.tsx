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
  const { phase, phaseError, apiBaseUrl, devFallbackEnabled, login, save, clear } = useAuth();
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
      <div style={outer}>
        <p style={{ color: '#6b7280', fontSize: 14 }}>
          {phase === 'validating' ? 'Verifying session…' : 'Loading…'}
        </p>
      </div>
    );
  }

  if (phase === 'invalid-config') {
    return (
      <div style={outer}>
        <div style={card}>
          <h1 style={title}>Configuration error</h1>
          <p style={subtitle}>{phaseError ?? 'Unable to load /config.json.'}</p>
          <p style={{ margin: 0, fontSize: 13, color: '#6b7280' }}>
            Ensure <code>/config.json</code> is served by the web server and contains valid OIDC settings.
          </p>
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
    <div style={outer}>
      <div style={card}>
        <h1 style={title}>FairSpot</h1>
        <p style={subtitle}>Sign in to access the employee portal.</p>

        {statusMessage ? (
          <p style={{ margin: 0, color: '#b91c1c', fontSize: 13 }}>{statusMessage}</p>
        ) : null}

        <button
          onClick={() => { void login(); }}
          style={primaryBtn}
        >
          Sign in
        </button>

        {devFallbackEnabled ? (
          <div style={{ marginTop: 8 }}>
            <button
              type="button"
              onClick={() => { setShowDevForm(v => !v); }}
              style={ghostBtn}
            >
              {showDevForm ? 'Hide' : 'Development access'}
            </button>

            {showDevForm ? (
              <form onSubmit={(e) => { void handleDevSave(e); }} style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 12 }}>
                <p style={{ margin: 0, fontSize: 12, color: '#6b7280' }}>
                  Development only — paste the token from the local smoke script.
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
                {formError ? <p style={{ margin: 0, color: '#b91c1c', fontSize: 13 }}>{formError}</p> : null}
                <button type="submit" disabled={saving} style={primaryBtn}>
                  {saving ? 'Verifying…' : 'Use token'}
                </button>
                <button type="button" onClick={clear} style={dangerBtn}>
                  Clear stored token
                </button>
              </form>
            ) : null}
          </div>
        ) : null}
      </div>
    </div>
  );
}

const outer: React.CSSProperties = {
  minHeight: '100vh',
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  background: '#f9fafb',
  padding: 16,
};
const card: React.CSSProperties = {
  background: '#fff',
  borderRadius: 12,
  border: '1px solid #e5e7eb',
  padding: 32,
  width: '100%',
  maxWidth: 440,
  display: 'flex',
  flexDirection: 'column',
  gap: 16,
};
const title: React.CSSProperties = { margin: 0, fontSize: 22, fontWeight: 700, color: '#111827' };
const subtitle: React.CSSProperties = { margin: 0, color: '#6b7280', fontSize: 14 };
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
const primaryBtn: React.CSSProperties = {
  background: '#1d4ed8',
  color: '#fff',
  border: 'none',
  borderRadius: 8,
  padding: '10px 0',
  fontSize: 14,
  fontWeight: 700,
  cursor: 'pointer',
};
const ghostBtn: React.CSSProperties = {
  background: 'none',
  color: '#6b7280',
  border: 'none',
  padding: 0,
  fontSize: 12,
  cursor: 'pointer',
  textDecoration: 'underline',
};
const dangerBtn: React.CSSProperties = {
  background: 'none',
  color: '#b91c1c',
  border: '1px solid #b91c1c',
  borderRadius: 8,
  padding: '8px 0',
  fontSize: 14,
  fontWeight: 600,
  cursor: 'pointer',
};
