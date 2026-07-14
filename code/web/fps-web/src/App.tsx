import { BrowserRouter, Navigate, NavLink, Route, Routes } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { useAuth } from './auth/AuthContext';
import {
  getSimulationStatus, advanceSimulation, resetSimulation, type SimulationStatus,
} from './api/simulation';
import {
  canAccessAudit,
  canAccessBookings,
  canAccessConfiguration,
  canAccessHrOperations,
  canAccessNotifications,
  canAccessParkingMap,
  canAccessProfile,
  canAccessReporting,
  canAccessTenantAdmin,
  canControlSimulation,
  defaultRoute,
  isPlatformPlane,
} from './auth/roles';
import { PlatformShell } from './platform/PlatformShell';
import { t, formatDateTime } from './i18n';
import { LocaleSwitcher } from './components/LocaleSwitcher';
import { TenantModulesProvider, useTenantModules } from './tenant/TenantModulesContext';
import { SeatOperationsPage } from './pages/SeatOperationsPage';
import { SessionPage } from './pages/SessionPage';
import { OidcCallbackPage } from './pages/OidcCallbackPage';
import { BookingsPage } from './pages/BookingsPage';
import { BookingHistoryPage } from './pages/BookingHistoryPage';
import { BookingDetailPage } from './pages/BookingDetailPage';
import { NewBookingPage } from './pages/NewBookingPage';
import { ProfilePage } from './pages/ProfilePage';
import { NotificationsPage } from './pages/NotificationsPage';
import { ReportingPage } from './pages/ReportingPage';
import { ConfigurationPage } from './pages/ConfigurationPage';
import { AuditPage } from './pages/AuditPage';
import { AuditorWorkspacePage } from './pages/AuditorWorkspacePage';
import { TenantAdminPage } from './pages/TenantAdminPage';
import { HrImportPage } from './pages/HrImportPage';
import { HrOperationsPage } from './pages/HrOperationsPage';
import { HrDrawHistoryPage } from './pages/HrDrawHistoryPage';
import { HrEmployeeHistoryPage } from './pages/HrEmployeeHistoryPage';
import { ParkingMapPage } from './pages/ParkingMapPage';
import { LegalPage } from './pages/LegalPage';
import { PilotPage } from './pages/PilotPage';
import { VerifyEmailPage } from './pages/VerifyEmailPage';

function Guard({ allowed, children }: { allowed: boolean; children: React.ReactNode }) {
  const { roles } = useAuth();
  if (!allowed) return <Navigate to={defaultRoute(roles)} replace />;
  return <>{children}</>;
}

// PLAT-seats (#710) — guard for seat routes. Unlike the plain Guard, it does NOT redirect while the
// tenant's modules are still loading, so a direct link / refresh to /seats on a Seats-enabled tenant
// isn't bounced away before GET /tenants/{id}/modules resolves. Redirect only once Seats is
// confirmed disabled (or the role isn't allowed).
function SeatsGuard({ roleAllowed, children }: { roleAllowed: boolean; children: React.ReactNode }) {
  const { roles } = useAuth();
  const { hasSeats, loading } = useTenantModules();
  if (!roleAllowed) return <Navigate to={defaultRoute(roles)} replace />;
  if (loading) return <div className="page-stack"><p className="plat-muted">{t('common.loading')}</p></div>;
  if (!hasSeats) return <Navigate to={defaultRoute(roles)} replace />;
  return <>{children}</>;
}

function AppFooter() {
  const { apiBaseUrl, bearerToken, environment, simulationEnabled, appVersion, roles } = useAuth();
  const cfg = { apiBaseUrl, bearerToken };
  const canControl = canControlSimulation(roles);
  const [sim, setSim] = useState<SimulationStatus | null>(null);
  const [simStatus, setSimStatus] = useState<'idle' | 'loading' | 'ok' | 'unavailable' | 'error'>('idle');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    if (!simulationEnabled) {
      setSim(null);
      setSimStatus('idle');
      return () => { cancelled = true; };
    }
    if (!bearerToken) {
      setSim(null);
      setSimStatus('loading');
      return () => { cancelled = true; };
    }
    setSimStatus('loading');
    void getSimulationStatus(cfg).then(r => {
      if (cancelled) return;
      if (r.kind === 'ok') {
        setSim(r.data);
        setSimStatus('ok');
        return;
      }
      setSim(null);
      setSimStatus(r.kind === 'not-available' ? 'unavailable' : 'error');
    });
    return () => { cancelled = true; };
  }, [simulationEnabled, apiBaseUrl, bearerToken]);

  async function handleAdvance(hours: number) {
    setBusy(true);
    const r = await advanceSimulation(cfg, hours);
    setBusy(false);
    if (r.kind === 'ok') setSim(r.data);
  }

  async function handleReset() {
    setBusy(true);
    const r = await resetSimulation(cfg);
    setBusy(false);
    if (r.kind === 'ok') setSim(r.data);
  }

  const hasContent = !!environment || simulationEnabled || !!appVersion;
  if (!hasContent) return null;

  function fmtTime(iso: string) {
    return formatDateTime(new Date(iso));
  }

  return (
    <footer className="app-footer">
      {environment && <span className="footer-env-badge">{environment}</span>}
      {appVersion && <span className="footer-version">v{appVersion}</span>}
      {sim?.simulationActive && (
        <span className="footer-sim-banner" title={t('footer.simulationBannerTitle')}>
          {t('footer.simulationBanner')}
        </span>
      )}
      {simulationEnabled && sim && (
        <span className="footer-real-time" title={t('footer.realTimeTitle')}>{t('footer.realTime', { time: fmtTime(sim.realNow) })}</span>
      )}
      {simulationEnabled && sim?.simulationActive && sim.virtualNow && (
        <span className="footer-sim-time" title={t('footer.simTimeTitle')} style={{ fontWeight: 600, color: 'var(--brand-primary)' }}>
          {t('footer.simTime', { time: fmtTime(sim.virtualNow) })}
        </span>
      )}
      {simulationEnabled && !sim?.simulationActive && sim && (
        <span className="footer-sim-state" style={{ color: '#6b7280', fontSize: '0.8rem' }}>
          {t('footer.simulationInactive')}
        </span>
      )}
      {simulationEnabled && canControl && simStatus === 'loading' && (
        <span className="footer-sim-state">{t('footer.simulationLoading')}</span>
      )}
      {simulationEnabled && canControl && simStatus === 'unavailable' && (
        <span className="footer-sim-state">{t('footer.simulationUnavailable')}</span>
      )}
      {simulationEnabled && canControl && simStatus === 'error' && (
        <span className="footer-sim-state">{t('footer.simulationUnreachable')}</span>
      )}
      {simulationEnabled && canControl && (
        <div className="footer-sim-controls">
          <button className="footer-sim-btn" disabled={busy} onClick={() => void handleAdvance(1)}>+1 h</button>
          <button className="footer-sim-btn" disabled={busy} onClick={() => void handleAdvance(8)}>+8 h</button>
          <button className="footer-sim-btn" disabled={busy} onClick={() => void handleReset()}>{t('footer.reset')}</button>
        </div>
      )}
    </footer>
  );
}

function Shell() {
  const { isConfigured, logout, branding, roles } = useAuth();
  // PLAT-seats (#710) — only surface seat nav entries when the tenant actually enables Seats.
  const { hasSeats } = useTenantModules();

  if (!isConfigured) return <Navigate to="/session" replace />;
  // A platform-plane identity has no tenant surfaces — send it to the operator console.
  if (isPlatformPlane(roles)) return <Navigate to="/platform/overview" replace />;

  const navItems = [
    // UX008 (#781) — one module-aware employee entry for reservation history/status.
    // Seat requesting stays reachable from My Reservations; no separate seats nav tree.
    canAccessBookings(roles) && { to: '/bookings', label: t('nav.myReservations') },
    // UX009 (#782) — one date-first employee Request entry for all enabled modules.
    canAccessBookings(roles) && { to: '/bookings/new', label: t('nav.request') },
    canAccessProfile(roles) && { to: '/profile', label: t('nav.profile') },
    canAccessNotifications(roles) && { to: '/notifications', label: t('nav.notifications') },
    canAccessParkingMap(roles) && { to: '/parking-map', label: t('nav.parkingMap') },
    canAccessHrOperations(roles) && { to: '/hr-operations', label: t('nav.parkingRequests') },
    canAccessHrOperations(roles) && hasSeats && { to: '/seat-operations', label: t('nav.seatRequests') },
    canAccessHrOperations(roles) && { to: '/hr-draw-history', label: t('nav.draws') },
    canAccessReporting(roles) && { to: '/reporting', label: t('nav.reports') },
    canAccessConfiguration(roles) && { to: '/configuration', label: t('nav.configuration') },
    canAccessConfiguration(roles) && { to: '/hr-import', label: t('nav.hrImport') },
    canAccessAudit(roles) && { to: '/auditor-workspace', label: t('nav.auditorWorkspace') },
    canAccessAudit(roles) && { to: '/audit', label: t('nav.auditConsole') },
    canAccessTenantAdmin(roles) && { to: '/tenant-admin', label: t('nav.admin') },
    { to: '/legal', label: t('nav.legal') },
  ].filter(Boolean) as { to: string; label: string }[];

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">
            {branding.logoUrl ? <img src={branding.logoUrl} alt="" /> : branding.productName.slice(0, 1)}
          </div>
          <div className="brand-title">
            <strong>{branding.productName}</strong>
            {branding.tenantName ? <span>{branding.tenantName}</span> : null}
          </div>
        </div>
        <nav className="app-nav" aria-label={t('nav.ariaLabel')}>
          {navItems.map(item => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => `nav-link${isActive ? ' nav-link-active' : ''}`}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <LocaleSwitcher />
        <button
          onClick={() => { void logout(); }}
          className="btn-danger"
        >
          {t('nav.signOut')}
        </button>
      </header>
      <main className="app-main">
        <Routes>
          <Route path="/bookings" element={<Guard allowed={canAccessBookings(roles)}><BookingsPage /></Guard>} />
          <Route path="/bookings/history" element={<Guard allowed={canAccessBookings(roles)}><BookingHistoryPage /></Guard>} />
          <Route path="/bookings/new" element={<Guard allowed={canAccessBookings(roles)}><NewBookingPage /></Guard>} />
          <Route path="/bookings/:requestId" element={<Guard allowed={canAccessBookings(roles)}><BookingDetailPage /></Guard>} />
          {/* UX009 (#782) — /seats stays as a compatibility deep link into the unified Request page. */}
          <Route path="/seats" element={<SeatsGuard roleAllowed={canAccessBookings(roles)}><Navigate to="/bookings/new?module=seats" replace /></SeatsGuard>} />
          <Route path="/seat-operations" element={<SeatsGuard roleAllowed={canAccessHrOperations(roles)}><SeatOperationsPage /></SeatsGuard>} />
          <Route path="/profile" element={<Guard allowed={canAccessProfile(roles)}><ProfilePage /></Guard>} />
          <Route path="/notifications" element={<Guard allowed={canAccessNotifications(roles)}><NotificationsPage /></Guard>} />
          <Route path="/reporting" element={<Guard allowed={canAccessReporting(roles)}><ReportingPage /></Guard>} />
          <Route path="/configuration" element={<Guard allowed={canAccessConfiguration(roles)}><ConfigurationPage /></Guard>} />
          <Route path="/hr-import" element={<Guard allowed={canAccessConfiguration(roles)}><HrImportPage /></Guard>} />
          <Route path="/parking-map" element={<Guard allowed={canAccessParkingMap(roles)}><ParkingMapPage /></Guard>} />
          <Route path="/hr-operations" element={<Guard allowed={canAccessHrOperations(roles)}><HrOperationsPage /></Guard>} />
          <Route path="/hr-operations/employees/:userId/history" element={<Guard allowed={canAccessHrOperations(roles)}><HrEmployeeHistoryPage /></Guard>} />
          <Route path="/hr-draw-history" element={<Guard allowed={canAccessHrOperations(roles)}><HrDrawHistoryPage /></Guard>} />
          <Route path="/auditor-workspace" element={<Guard allowed={canAccessAudit(roles)}><AuditorWorkspacePage /></Guard>} />
          <Route path="/audit" element={<Guard allowed={canAccessAudit(roles)}><AuditPage /></Guard>} />
          <Route path="/tenant-admin" element={<Guard allowed={canAccessTenantAdmin(roles)}><TenantAdminPage /></Guard>} />
          <Route path="*" element={<Navigate to={defaultRoute(roles)} replace />} />
        </Routes>
      </main>
      <AppFooter />
    </div>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/session" element={<SessionPage />} />
        <Route path="/auth/callback" element={<OidcCallbackPage />} />
        {/* AUTH008B (#734) — verification landing must be top-level so the emailed link isn't bounced to
            /session (which would discard the token) before the callback can consume and scrub it. */}
        <Route path="/verify-email" element={<VerifyEmailPage />} />
        <Route path="/legal" element={<LegalPage />} />
        <Route path="/pilot" element={<PilotPage />} />
        <Route path="/platform/*" element={<PlatformShell />} />
        <Route path="/*" element={<TenantModulesProvider><Shell /></TenantModulesProvider>} />
      </Routes>
    </BrowserRouter>
  );
}
