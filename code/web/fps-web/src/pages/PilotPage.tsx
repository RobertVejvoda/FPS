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
const REPO_URL = 'https://github.com/RobertVejvoda/fairspot';

type SubmitState = 'idle' | 'sending';
type Scenario = 'Parking' | 'Seats' | 'Both';

// Public, unauthenticated FairSpot entry page (SITE002). One-page scroll: positive
// positioning, parking as the working scenario, seats/desks as planned, and a
// request-based pilot form wired to the tenant-request intake. No live demo, no
// instant provisioning, no customer-facing AI messaging.
export function PilotPage() {
  const { apiBaseUrl, branding, turnstileSiteKey } = useAuth();

  const [companyName, setCompanyName] = useState('');
  const [companyDomain, setCompanyDomain] = useState('');
  const [workEmail, setWorkEmail] = useState('');
  const [scenario, setScenario] = useState<Scenario>('Parking');
  const [employees, setEmployees] = useState('');
  const [locations, setLocations] = useState('');
  const [message, setMessage] = useState('');
  const [verifyToken, setVerifyToken] = useState('');
  const [error, setError] = useState('');
  const [state, setState] = useState<SubmitState>('idle');
  const [reference, setReference] = useState<string | null>(null);

  const widgetHost = useRef<HTMLDivElement | null>(null);
  const widgetId = useRef<string | null>(null);

  useEffect(() => {
    document.title = `${branding.productName} — fair allocation for limited workplace resources`;
  }, [branding.productName]);

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

  // Fold the structured business context into the free-text message so the existing
  // tenant-request intake captures it without a backend DTO change.
  function composeMessage(): string {
    const lines = [
      `Resource scenario: ${scenario}`,
      employees.trim() ? `Approx. employees: ${employees.trim()}` : null,
      locations.trim() ? `Locations: ${locations.trim()}` : null,
      '',
      message.trim() || '(no additional message)',
    ];
    return lines.filter((l) => l !== null).join('\n');
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!companyName.trim() || !companyDomain.trim() || !workEmail.trim()) {
      setError('Please fill in your company name, domain, and work email so we can set up your pilot.');
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
      message: composeMessage(),
      verificationToken: verifyToken,
    });
    setState('idle');

    if (result.kind === 'ok') {
      setReference(result.reference);
      window.scrollTo({ top: 0, behavior: 'smooth' });
      return;
    }
    if (widgetId.current && window.turnstile) { window.turnstile.reset(widgetId.current); setVerifyToken(''); }
    if (result.kind === 'invalid') setError(result.message);
    else if (result.kind === 'rate-limited') {
      setError("We've had several requests from your network recently. Please wait a few minutes and try again.");
    } else {
      setError("We couldn't send your request just now. Please try again in a moment.");
    }
  }

  return (
    <div className="site">
      <header className="site-topbar">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">
            {branding.logoUrl ? <img src={branding.logoUrl} alt="" /> : branding.productName.slice(0, 1)}
          </div>
          <div className="brand-title"><strong>{branding.productName}</strong></div>
        </div>
        <nav className="site-topnav">
          <a href="#how">How it works</a>
          <a href="#start" className="btn-green btn-green-sm">Start a free pilot</a>
        </nav>
      </header>

      {/* Hero */}
      <section className="site-hero">
        <div className="site-hero-inner">
          <p className="site-eyebrow">Fair allocation for limited workplace resources</p>
          <h1>Give everyone a fair shot at limited parking.</h1>
          <p className="site-lede">
            Clear rules and a transparent, automated allocation decide every space — and every
            outcome is easy to explain. The same model extends to desks and seats.
          </p>
          <div className="site-cta-row">
            <a href="#start" className="btn-green">Start a free pilot</a>
            <a href="#how" className="btn-outline">See how it works</a>
            <a href={REPO_URL} className="site-link-quiet" target="_blank" rel="noopener noreferrer">Run it yourself ↗</a>
          </div>
          <p className="site-hero-note">Open source · request-based pilot · synthetic demo data only</p>
        </div>
      </section>

      {/* Opportunity */}
      <section className="site-section">
        <h2>What a fair allocation gives you</h2>
        <div className="site-grid site-grid-3">
          <div className="site-card">
            <h3>A fair shot for everyone</h3>
            <p>Limited spaces go by clear, consistent rules — not by who emailed first or shouted loudest.</p>
          </div>
          <div className="site-card">
            <h3>Outcomes you can explain</h3>
            <p>Every allocation is transparent and backed by evidence, so HR can answer "why didn't I get a spot?" with confidence.</p>
          </div>
          <div className="site-card">
            <h3>Time back for HR &amp; facilities</h3>
            <p>Requests, allocation, and notifications run automatically. Management gets clear usage insight.</p>
          </div>
        </div>
      </section>

      {/* How it works — parking today */}
      <section className="site-section site-section-alt" id="how">
        <h2>How it works — parking, today</h2>
        <p className="site-section-lede">Parking is the working scenario in FairSpot today. Here's the flow:</p>
        <ol className="site-steps">
          <li><strong>Employees request a spot</strong> for the days they need it.</li>
          <li><strong>Clear rules &amp; a cut-off time</strong> are set by HR — caps, priorities, accessibility, EV, company cars.</li>
          <li><strong>A fair automated allocation</strong> assigns limited spaces; guaranteed holders are honored first, the rest by transparent fairness.</li>
          <li><strong>Everyone sees the outcome</strong> — allocated, waitlisted, or a clearly explained reason.</li>
          <li><strong>Audit evidence is kept</strong> for every decision, so it can be reviewed later.</li>
        </ol>
        <p className="site-note">Want a closer look? We'll walk you through a real Green Logistics example during your pilot — no sign-up wall, no canned sales demo.</p>
      </section>

      {/* Why it's fair (trust) */}
      <section className="site-section">
        <h2>Why teams trust it</h2>
        <div className="site-grid site-grid-2">
          <div className="site-card">
            <h3>Transparent rules, not a black box</h3>
            <p>Allocation follows documented rules you can read and explain — no opaque scoring deciding who parks.</p>
          </div>
          <div className="site-card">
            <h3>Audit evidence</h3>
            <p>Every request and decision is recorded, so outcomes hold up to questions and review.</p>
          </div>
          <div className="site-card">
            <h3>Your data stays yours</h3>
            <p>Your company's data stays separate and private, with data-removal support. Built for company data ownership.</p>
          </div>
          <div className="site-card">
            <h3>Open source, no lock-in</h3>
            <p>The fairness engine is open and inspectable — run it yourself, or let us host it. Either way, no black box.</p>
          </div>
        </div>
      </section>

      {/* Who benefits */}
      <section className="site-section site-section-alt">
        <h2>Who it's for</h2>
        <div className="site-grid site-grid-4">
          <div className="site-card"><h3>Employees</h3><p>A fair shot at a spot, clear status, and their own request history.</p></div>
          <div className="site-card"><h3>HR</h3><p>Less manual coordination and an easy answer for every outcome.</p></div>
          <div className="site-card"><h3>Admins &amp; facilities</h3><p>Configure locations, rules, resources, and branding for your company.</p></div>
          <div className="site-card"><h3>Auditors &amp; management</h3><p>Evidence and usage insight to review fairness and plan capacity.</p></div>
        </div>
      </section>

      {/* What's next */}
      <section className="site-section">
        <span className="site-badge">Planned</span>
        <h2>Next: desks &amp; seats</h2>
        <p className="site-section-lede">
          Hybrid teams need predictable workspace access. The same request, rules, allocation, and
          evidence model that powers parking today will extend to shared desks and seats. Parking
          works now; desks and seats are on the roadmap, not yet available.
        </p>
      </section>

      {/* Get started — pilot form */}
      <section className="site-section site-section-alt" id="start">
        <h2>Start a free pilot</h2>
        {reference ? (
          <div className="site-success" role="status">
            <h3>Thanks — we're on it.</h3>
            <p>
              We've received your request and will be in touch to set up your FairSpot pilot
              workspace. Your reference is <strong>{reference}</strong>.
            </p>
          </div>
        ) : (
          <>
            <p className="site-section-lede">
              Tell us a little about your company and we'll prepare a guided evaluation. We review
              every request before setting up access — no instant sign-up, no spam.
            </p>
            <form onSubmit={(e) => { void handleSubmit(e); }} className="site-form">
              <div className="site-form-row">
                <label className="site-label">
                  Company name
                  <input className="site-input" value={companyName} onChange={(e) => setCompanyName(e.target.value)} placeholder="Green Logistics" autoComplete="organization" />
                </label>
                <label className="site-label">
                  Company domain
                  <input className="site-input" value={companyDomain} onChange={(e) => setCompanyDomain(e.target.value)} placeholder="yourcompany.com" autoCapitalize="none" autoComplete="off" />
                </label>
              </div>
              <div className="site-form-row">
                <label className="site-label">
                  Work email
                  <input className="site-input" type="email" value={workEmail} onChange={(e) => setWorkEmail(e.target.value)} placeholder="you@yourcompany.com" autoCapitalize="none" autoComplete="email" />
                </label>
                <label className="site-label">
                  What would you like to allocate?
                  <select className="site-input" value={scenario} onChange={(e) => setScenario(e.target.value as Scenario)}>
                    <option value="Parking">Parking (available now)</option>
                    <option value="Seats">Desks / seats (planned)</option>
                    <option value="Both">Both</option>
                  </select>
                </label>
              </div>
              <div className="site-form-row">
                <label className="site-label">
                  Approx. employees
                  <input className="site-input" inputMode="numeric" value={employees} onChange={(e) => setEmployees(e.target.value)} placeholder="e.g. 120" />
                </label>
                <label className="site-label">
                  Number of locations <span className="site-optional">(optional)</span>
                  <input className="site-input" inputMode="numeric" value={locations} onChange={(e) => setLocations(e.target.value)} placeholder="e.g. 1" />
                </label>
              </div>
              <label className="site-label">
                Anything else? <span className="site-optional">(optional)</span>
                <textarea className="site-input site-textarea" value={message} onChange={(e) => setMessage(e.target.value)} placeholder="What would you like to evaluate?" rows={3} />
              </label>

              {turnstileSiteKey ? <div className="site-verify" ref={widgetHost} /> : null}
              {error ? <p className="site-error" role="alert">{error}</p> : null}

              <button type="submit" disabled={state === 'sending'} className="btn-green site-submit">
                {state === 'sending' ? 'Sending…' : 'Start a free pilot'}
              </button>
              <p className="site-fineprint">We use your details only to prepare your evaluation and get in touch.</p>
            </form>
          </>
        )}
      </section>

      <footer className="site-footer">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">
            {branding.logoUrl ? <img src={branding.logoUrl} alt="" /> : branding.productName.slice(0, 1)}
          </div>
          <div className="brand-title"><strong>{branding.productName}</strong></div>
        </div>
        <nav className="site-footer-links">
          <a href={REPO_URL} target="_blank" rel="noopener noreferrer">Open source (GitHub)</a>
          <Link to="/legal">Legal notices</Link>
          <a href="#start">Start a free pilot</a>
        </nav>
        <p className="site-footer-note">Open core, AGPL-licensed. FairSpot allocates scarce workplace resources fairly and transparently.</p>
      </footer>
    </div>
  );
}
