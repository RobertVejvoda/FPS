import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth, type AuthPhase } from '../auth/AuthContext';

const FAILURE_PHASES: AuthPhase[] = [
  'login-cancelled',
  'login-failed',
  'session-expired',
  'unreachable',
  'invalid-config',
];

export function OidcCallbackPage() {
  const { phase } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (phase === 'authenticated') {
      navigate('/bookings', { replace: true });
    } else if (FAILURE_PHASES.includes(phase)) {
      navigate('/session', { replace: true });
    }
  }, [phase, navigate]);

  return (
    <div style={outer}>
      <p style={{ color: '#6b7280', fontSize: 14 }}>Completing sign in…</p>
    </div>
  );
}

const outer: React.CSSProperties = {
  minHeight: '100vh',
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  background: '#f9fafb',
};
