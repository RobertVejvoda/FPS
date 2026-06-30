import { useEffect } from 'react';
import { Navigate, NavLink, Route, Routes } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { canAccessPlatformConsole, canTriagePlatformOnboarding, formatRoles } from '../auth/roles';
import { PlatformAccessDenied } from './PlatformAccessDenied';
import { PlatformOverview } from './PlatformOverview';
import { PlatformPlaceholderPage } from './PlatformPlaceholderPage';
import { TenantsDirectoryPage } from './TenantsDirectoryPage';
import { TenantDetailPage } from './TenantDetailPage';

// Active nav targets in this shell slice. Order follows the design's left-nav
// (platform-dashboard-ux.md §3): Overview · Tenants · Onboarding · Health · … · Audit.
const NAV = [
  { to: '/platform/overview', label: 'Overview' },
  { to: '/platform/tenants', label: 'Tenants' },
  { to: '/platform/onboarding', label: 'Onboarding' },
  { to: '/platform/health', label: 'Health' },
  { to: '/platform/audit', label: 'Audit' },
];

// Future destinations, rendered disabled with a tooltip naming the dependency slice so
// reviewers see they are planned, not broken (they depend on slices not yet built).
const FUTURE_NAV = [
  { label: 'Usage', slice: 'PLAT005 — usage & cost ledger' },
  { label: 'Demo', slice: 'PLAT003 — demo sandbox reset' },
  { label: 'Feedback', slice: 'PLAT006 — feedback / beta program' },
];

// FairSpot operator console (PLAT008A). A separate surface from the tenant app, suitable for
// platform.<domain>: operator branding only (never tenant branding/theme), a platform-issuer
// auth gate, and role-aware navigation. The .platform-shell wrapper scopes its own brand
// palette (styles.css) so nothing here resolves the tenant-loaded --brand-* variables.
export function PlatformShell() {
  const { isConfigured, roles, logout } = useAuth();

  // The operator console owns its document title — never the tenant productName | tenantName.
  useEffect(() => {
    const previous = document.title;
    document.title = 'FairSpot · Platform';
    return () => { document.title = previous; };
  }, []);

  if (!isConfigured) return <Navigate to="/session" replace />;

  // Auth gate: only a platform-plane identity may reach the console. A tenant/customer token
  // (even tenant admin) carries no platform_* role and is rejected with a clear state.
  if (!canAccessPlatformConsole(roles)) return <PlatformAccessDenied />;

  const canTriage = canTriagePlatformOnboarding(roles);

  return (
    <div className="platform-shell app-shell">
      <header className="app-header">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">
            <img src="/brand/fairspot-app-icon.svg" alt="" />
          </div>
          <div className="brand-title">
            <strong>FairSpot</strong>
            <span>Platform operator</span>
          </div>
        </div>
        <nav className="app-nav" aria-label="Platform navigation">
          {NAV.map(item => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => `nav-link${isActive ? ' nav-link-active' : ''}`}
            >
              {item.label}
            </NavLink>
          ))}
          {FUTURE_NAV.map(item => (
            <span
              key={item.label}
              className="nav-link nav-link-disabled"
              aria-disabled="true"
              title={`Planned — not wired yet (${item.slice})`}
            >
              {item.label}
              <span aria-hidden="true"> ·</span>
            </span>
          ))}
        </nav>
        <span className="plat-role-badge" title="Your platform role(s)">{formatRoles(roles)}</span>
        <button onClick={() => { void logout(); }} className="btn-danger">Sign out</button>
      </header>
      <main className="app-main">
        <Routes>
          <Route index element={<Navigate to="/platform/overview" replace />} />
          <Route path="overview" element={<PlatformOverview />} />
          <Route path="tenants" element={<TenantsDirectoryPage />} />
          <Route path="tenants/:tenantId" element={<TenantDetailPage />} />
          <Route
            path="onboarding"
            element={(
              <PlatformPlaceholderPage
                title="Onboarding queue"
                description="Tenant-request triage funnel (Requested → Approved → Provisioning → Ready)."
                slice="PLAT008C"
              >
                {canTriage ? (
                  <p className="plat-muted">
                    Approve / reject controls (operator &amp; admin) will appear here once the
                    TenantRequest queue is wired.
                  </p>
                ) : (
                  <p className="plat-muted">
                    Read-only for <strong>platform_auditor</strong>: triage actions are limited to
                    platform_operator and platform_admin.
                  </p>
                )}
              </PlatformPlaceholderPage>
            )}
          />
          <Route
            path="health"
            element={(
              <PlatformPlaceholderPage
                title="Platform health"
                description="Red-flags and operational health across the platform."
                slice="PLAT008D"
              />
            )}
          />
          <Route
            path="audit"
            element={(
              <PlatformPlaceholderPage
                title="Audit"
                description="Cross-tenant operator audit evidence."
                slice="Audit — deep view wired later"
              />
            )}
          />
          <Route path="*" element={<Navigate to="/platform/overview" replace />} />
        </Routes>
      </main>
    </div>
  );
}
