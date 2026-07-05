import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { confirmEmailVerification } from '../api/emailVerification';

// AUTH008B (#734) — landing page for the emailed verification link
// (https://app.fairspot.net/verify-email?token=…). The token is the Secret: this page reads it once from
// the query string, immediately strips it from the URL (history replacement) so it never lingers in the
// address bar / browser history / any onward navigation, and confirms it by POSTing it in the request body
// to the authenticated /profile/email/verification/confirm endpoint. The token is held only in memory and
// is never written to storage.
type Phase =
  | 'reading'      // pulling the token out of the URL
  | 'no-token'     // link had no token
  | 'need-signin'  // confirmation requires the account's own session
  | 'verifying'    // confirm request in flight
  | 'verified'
  | 'rejected'
  | 'error';

export function VerifyEmailPage() {
  const { isConfigured, apiBaseUrl, bearerToken } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [phase, setPhase] = useState<Phase>('reading');
  const [reason, setReason] = useState<string | null>(null);
  // Token lives only in component state (memory) — never written to storage.
  const [token, setToken] = useState<string | null>(null);
  const [tokenRead, setTokenRead] = useState(false);

  // Step 1 (once): capture the token, then scrub it from the URL before anything else can navigate.
  useEffect(() => {
    const captured = new URLSearchParams(location.search).get('token');
    if (location.search) navigate(location.pathname, { replace: true });
    setToken(captured);
    setTokenRead(true);
    // Intentionally runs once on mount; the captured search string is the initial URL.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Step 2: confirm once we have read the token and have an authenticated session.
  useEffect(() => {
    if (!tokenRead) return;
    if (token === null) { setPhase('no-token'); return; }
    if (!isConfigured) { setPhase('need-signin'); return; }

    let cancelled = false;
    setPhase('verifying');
    void confirmEmailVerification({ apiBaseUrl, bearerToken }, token).then(result => {
      if (cancelled) return;
      switch (result.kind) {
        case 'verified': setPhase('verified'); break;
        case 'unauthenticated': setPhase('need-signin'); break;
        case 'rejected': setReason(result.reason); setPhase('rejected'); break;
        default: setPhase('error'); break;
      }
    });
    return () => { cancelled = true; };
  }, [tokenRead, token, isConfigured, apiBaseUrl, bearerToken]);

  return (
    <div className="legal-page">
      <div className="legal-panel">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">F</div>
          <div className="brand-title">
            <strong>FairSpot</strong>
            <span>Email verification</span>
          </div>
        </div>
        <h1>Confirm your email address</h1>
        {renderBody(phase, reason)}
        <div className="legal-actions">
          <Link className="btn-primary" to="/session">Go to FairSpot</Link>
        </div>
      </div>
    </div>
  );
}

function renderBody(phase: Phase, reason: string | null) {
  switch (phase) {
    case 'reading':
    case 'verifying':
      return <p className="plat-muted">Confirming your email address…</p>;
    case 'verified':
      return <p>Your email address is confirmed. You can now receive FairSpot notifications.</p>;
    case 'need-signin':
      return (
        <p>
          Please sign in to FairSpot with this account, then open the verification link from your email
          again to confirm. Confirmation is tied to your own signed-in session.
        </p>
      );
    case 'rejected':
      return (
        <p>
          This verification link is no longer valid{reason === 'expired' ? ' — it has expired' : ''}. Request a
          new verification email from your FairSpot profile and try again.
        </p>
      );
    case 'no-token':
      return <p>This link is missing its verification code. Open the link directly from your FairSpot email.</p>;
    default:
      return <p>We couldn’t confirm your email right now. Please try the link again shortly.</p>;
  }
}
