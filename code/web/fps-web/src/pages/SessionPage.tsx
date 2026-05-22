import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function SessionPage() {
  const { apiBaseUrl, bearerToken, save, clear } = useAuth();
  const navigate = useNavigate();
  const [urlInput, setUrlInput] = useState(apiBaseUrl);
  const [tokenInput, setTokenInput] = useState(bearerToken);
  const [error, setError] = useState('');

  function handleSave(e: React.FormEvent) {
    e.preventDefault();
    if (!urlInput.trim() || !tokenInput.trim()) {
      setError('Both fields are required.');
      return;
    }
    save(urlInput, tokenInput);
    navigate('/bookings');
  }

  return (
    <div style={outer}>
      <div style={card}>
        <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700 }}>FairSpot Employee Portal</h1>
        <p style={{ margin: 0, color: '#6b7280', fontSize: 14 }}>
          Enter the API base URL and a bearer token from a running backend.
          Real OIDC login is configured via environment variables in later slices.
        </p>
        <form onSubmit={handleSave} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <label style={labelStyle}>
            API base URL
            <input
              value={urlInput}
              onChange={(e) => setUrlInput(e.target.value)}
              placeholder="http://localhost:5100"
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
          {error ? <p style={{ margin: 0, color: '#b91c1c', fontSize: 13 }}>{error}</p> : null}
          <button type="submit" style={primaryBtn}>Save and continue</button>
          {bearerToken ? (
            <button type="button" onClick={clear} style={dangerBtn}>Clear stored session</button>
          ) : null}
        </form>
      </div>
    </div>
  );
}

const outer: React.CSSProperties = { minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#f9fafb', padding: 16 };
const card: React.CSSProperties = { background: '#fff', borderRadius: 12, border: '1px solid #e5e7eb', padding: 32, width: '100%', maxWidth: 480, display: 'flex', flexDirection: 'column', gap: 16 };
const labelStyle: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 4, fontSize: 13, fontWeight: 600, color: '#111827' };
const inputStyle: React.CSSProperties = { border: '1px solid #e5e7eb', borderRadius: 6, padding: '8px 12px', fontSize: 14, color: '#111827', background: '#fff', width: '100%', boxSizing: 'border-box' };
const primaryBtn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 8, padding: '10px 0', fontSize: 14, fontWeight: 700, cursor: 'pointer' };
const dangerBtn: React.CSSProperties = { background: 'none', color: '#b91c1c', border: '1px solid #b91c1c', borderRadius: 8, padding: '8px 0', fontSize: 14, fontWeight: 600, cursor: 'pointer' };
