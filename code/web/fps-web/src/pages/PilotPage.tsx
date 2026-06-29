import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { submitPilotRequest } from '../api/customer';

// Minimal typing for the Cloudflare bot-protection widget loaded at runtime.
declare global {
  interface Window {
    turnstile?: {
      render: (el: HTMLElement, opts: {
        sitekey: string;
        callback?: (token: string) => void;
        'error-callback'?: () => void;
        'expired-callback'?: () => void;
      }) => string;
      remove: (id: string) => void;
      reset: (id?: string) => void;
    };
  }
}

const VERIFY_SCRIPT = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

type SubmitState = 'idle' | 'sending';

// Public, unauthenticated "Start a FairSpot Pilot" page (PLAT004c). A business visitor can
// explore the Green Logistics demo or request a guided evaluation workspace for their company.
// Copy is intentionally business-facing; no prospect details are stored in the browser.
export function PilotPage() {
  const { apiBaseUrl, branding, turnstileSiteKey, demoUrl } = useAuth();

  const [companyName, setCompanyName] = useState('');
  const [companyDomain, setCompanyDomain] = useState('');
  const [workEmail, setWorkEmail] = useState('');
  const [challenge, setChallenge] = useState('');
  const [verifyToken, setVerifyToken] = useState('');
  const [error, setError] = useState('');
  const [state, setState] = useState<SubmitState>('idle');
  const [reference, setReference] = useState<string | null>(null);

  const widgetHost = useRef<HTMLDivElement | null>(null);
  const widgetId = useRef<string | null>(null);

  // Load and render the bot-protection widget only when a site key is configured (it is left
  // empty in local/dev, where the API skips verification). Scoped to this public page so the
  // third-party script never loads inside the authenticated app.
  useEffect(() => {
    if (!turnstileSiteKey || !widgetHost.current) return;

    function renderWidget() {
      if (!window.turnstile || !widgetHost.current || widgetId.current) return;
      widgetId.current = window.turnstile.render(widgetHost.current, {
        sitekey: turnstileSiteKey,
        callback: (token) => { setVerifyToken(token); setError(''); },
        'error-callback': () => setVerifyToken(''),
        'expired-callback': () => setVerifyToken(''),
      });
    }

    if (window.turnstile) {
      renderWidget();
      return;
    }
    let script = document.querySelector<HTMLScriptElement>('script[data-fps-verify]');
    if (!script) {
      script = document.createElement('script');
      script.src = VERIFY_SCRIPT;
      script.async = true;
      script.defer = true;
      script.dataset.fpsVerify = 'true';
      document.head.appendChild(script);
    }
    script.addEventListener('load', renderWidget);
    return () => {
      script?.removeEventListener('load', renderWidget);
      if (widgetId.current && window.turnstile) {
        window.turnstile.remove(widgetId.current);
        widgetId.current = null;
      }
    };
  }, [turnstileSiteKey]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!companyName.trim() || !companyDomain.trim() || !workEmail.trim() || !challenge.trim()) {
      setError('Please fill in every field so we can prepare your workspace.');
      return;
    }
    if (turnstileSiteKey && !verifyToken) {
      setError('Please complete the verification check below.');
      return;
    }
    setError('');
    setState('sending');
    const result = await submitPilotRequest(apiBaseUrl, {
      companyName: companyName.trim(),
      companyDomain: companyDomain.trim(),
      workEmail: workEmail.trim(),
      message: challenge.trim(),
      verificationToken: verifyToken,
    });
    setState('idle');

    if (result.kind === 'ok') {
      setReference(result.reference);
      return;
    }
    // Reset the widget so the visitor can retry with a fresh check.
    if (widgetId.current && window.turnstile) { window.turnstile.reset(widgetId.current); setVerifyToken(''); }
    if (result.kind === 'invalid') setError(result.message);
    else if (result.kind === 'rate-limited') {
      setError("We've had several requests from your network recently. Please wait a few minutes and try again.");
    } else {
      setError("We couldn't send your request just now. Please try again in a moment.");
    }
  }

  return (
    <div className="session-shell">
      <div className="session-story">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">
            {branding.logoUrl ? <img src={branding.logoUrl} alt="" /> : branding.productName.slice(0, 1)}
          </div>
          <div className="brand-title"><strong>{branding.productName}</strong></div>
        </div>
        <div>
          <p className="session-eyebrow">Start a FairSpot Pilot</p>
          <h1>Fair workplace parking, ready to evaluate.</h1>
          <p>Evaluate FairSpot with a guided workspace for your company — see fair allocation and transparent results with your own parking challenge in mind.</p>
          <ul className="pilot-values">
            <li>Fair parking allocation without spreadsheets</li>
            <li>Transparent Draw results everyone can understand</li>
            <li>HR and facility control with audit evidence</li>
            <li>A guided evaluation workspace set up for your company</li>
          </ul>
          <a className="btn-secondary pilot-demo-cta" href={demoUrl}>Explore the Green Logistics demo</a>
        </div>
        <Link className="session-legal-link" to="/legal">Legal notices</Link>
      </div>

      <div className="session-panel-wrap">
        <div className="session-panel">
          {reference ? (
            <div className="pilot-success" role="status">
              <h2>Thanks — we're on it.</h2>
              <p>
                We received your request and will contact you to prepare your FairSpot pilot
                workspace. In the meantime, you're welcome to explore the Green Logistics demo.
              </p>
              <a className="btn-primary pilot-demo-cta" href={demoUrl}>Explore the Green Logistics demo</a>
            </div>
          ) : (
            <>
              <h2>Request your pilot</h2>
              <p>Tell us a little about your company and we'll set up a guided evaluation.</p>
              <form onSubmit={(e) => { void handleSubmit(e); }} className="pilot-form">
                <label className="pilot-label">
                  Company name
                  <input
                    className="pilot-input"
                    value={companyName}
                    onChange={(e) => setCompanyName(e.target.value)}
                    placeholder="Green Logistics"
                    autoComplete="organization"
                  />
                </label>
                <label className="pilot-label">
                  Company domain
                  <input
                    className="pilot-input"
                    value={companyDomain}
                    onChange={(e) => setCompanyDomain(e.target.value)}
                    placeholder="yourcompany.com"
                    autoCapitalize="none"
                    autoComplete="off"
                  />
                </label>
                <label className="pilot-label">
                  Work email
                  <input
                    className="pilot-input"
                    type="email"
                    value={workEmail}
                    onChange={(e) => setWorkEmail(e.target.value)}
                    placeholder="you@yourcompany.com"
                    autoCapitalize="none"
                    autoComplete="email"
                  />
                </label>
                <label className="pilot-label">
                  Tell us about your parking challenge
                  <textarea
                    className="pilot-input pilot-textarea"
                    value={challenge}
                    onChange={(e) => setChallenge(e.target.value)}
                    placeholder="What would you like to evaluate? How many sites or spaces are involved?"
                    rows={4}
                  />
                </label>

                {turnstileSiteKey ? <div className="pilot-verify" ref={widgetHost} /> : null}

                {error ? <p className="pilot-error">{error}</p> : null}

                <button type="submit" disabled={state === 'sending'} className="btn-primary pilot-submit">
                  {state === 'sending' ? 'Sending…' : 'Start a FairSpot Pilot'}
                </button>
                <p className="pilot-fineprint">
                  We use your details only to prepare your evaluation and get in touch.
                </p>
              </form>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
