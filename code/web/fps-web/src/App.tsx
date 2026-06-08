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
  canAccessProfile,
  canAccessReporting,
  canAccessTenantAdmin,
  canControlSimulation,
  defaultRoute,
} from './auth/roles';
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
import { TenantAdminPage } from './pages/TenantAdminPage';
import { HrImportPage } from './pages/HrImportPage';
import { HrOperationsPage } from './pages/HrOperationsPage';
import { HrDrawHistoryPage } from './pages/HrDrawHistoryPage';
import { LegalPage } from './pages/LegalPage';

function Guard({ allowed, children }: { allowed: boolean; children: React.ReactNode }) {
  const { roles } = useAuth();
  if (!allowed) return <Navigate to={defaultRoute(roles)} replace />;
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
    return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
  }

  return (
    <footer className="app-footer">
      {environment && <span className="footer-env-badge">{environment}</span>}
      {appVersion && <span className="footer-version">v{appVersion}</span>}
      {sim?.simulationActive && <span className="footer-sim-banner">NON-PRODUCTION SIMULATION</span>}
      {simulationEnabled && sim && (
        <span className="footer-real-time">Real: {fmtTime(sim.realNow)}</span>
      )}
      {simulationEnabled && sim?.simulationActive && sim.virtualNow && (
        <span className="footer-sim-time">Sim: {fmtTime(sim.virtualNow)}</span>
      )}
      {simulationEnabled && canControl && simStatus === 'loading' && (
        <span className="footer-sim-state">Loading simulation clock...</span>
      )}
      {simulationEnabled && canControl && simStatus === 'unavailable' && (
        <span className="footer-sim-state">Simulation clock unavailable</span>
      )}
      {simulationEnabled && canControl && simStatus === 'error' && (
        <span className="footer-sim-state">Simulation clock not reachable</span>
      )}
      {simulationEnabled && canControl && (
        <div className="footer-sim-controls">
          <button className="footer-sim-btn" disabled={busy} onClick={() => void handleAdvance(1)}>+1 h</button>
          <button className="footer-sim-btn" disabled={busy} onClick={() => void handleAdvance(8)}>+8 h</button>
          <button className="footer-sim-btn" disabled={busy} onClick={() => void handleReset()}>Reset</button>
        </div>
      )}
    </footer>
  );
}

function Shell() {
  const { isConfigured, logout, branding, roles } = useAuth();

  if (!isConfigured) return <Navigate to="/session" replace />;

  const navItems = [
    canAccessBookings(roles) && { to: '/bookings', label: 'My Spots' },
    canAccessProfile(roles) && { to: '/profile', label: 'Profile' },
    canAccessNotifications(roles) && { to: '/notifications', label: 'Notifications' },
    canAccessHrOperations(roles) && { to: '/hr-operations', label: 'HR Operations' },
    canAccessHrOperations(roles) && { to: '/hr-draw-history', label: 'Draw History' },
    canAccessReporting(roles) && { to: '/reporting', label: 'Reports' },
    canAccessConfiguration(roles) && { to: '/configuration', label: 'Configuration' },
    canAccessConfiguration(roles) && { to: '/hr-import', label: 'HR Import' },
    canAccessAudit(roles) && { to: '/audit', label: 'Audit' },
    canAccessTenantAdmin(roles) && { to: '/tenant-admin', label: 'Admin' },
    { to: '/legal', label: 'Legal' },
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
        <nav className="app-nav" aria-label="Main navigation">
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
        <button
          onClick={() => { void logout(); }}
          className="btn-danger"
        >
          Sign out
        </button>
      </header>
      <main className="app-main">
        <Routes>
          <Route path="/bookings" element={<Guard allowed={canAccessBookings(roles)}><BookingsPage /></Guard>} />
          <Route path="/bookings/history" element={<Guard allowed={canAccessBookings(roles)}><BookingHistoryPage /></Guard>} />
          <Route path="/bookings/new" element={<Guard allowed={canAccessBookings(roles)}><NewBookingPage /></Guard>} />
          <Route path="/bookings/:requestId" element={<Guard allowed={canAccessBookings(roles)}><BookingDetailPage /></Guard>} />
          <Route path="/profile" element={<Guard allowed={canAccessProfile(roles)}><ProfilePage /></Guard>} />
          <Route path="/notifications" element={<Guard allowed={canAccessNotifications(roles)}><NotificationsPage /></Guard>} />
          <Route path="/reporting" element={<Guard allowed={canAccessReporting(roles)}><ReportingPage /></Guard>} />
          <Route path="/configuration" element={<Guard allowed={canAccessConfiguration(roles)}><ConfigurationPage /></Guard>} />
          <Route path="/hr-import" element={<Guard allowed={canAccessConfiguration(roles)}><HrImportPage /></Guard>} />
          <Route path="/hr-operations" element={<Guard allowed={canAccessHrOperations(roles)}><HrOperationsPage /></Guard>} />
          <Route path="/hr-draw-history" element={<Guard allowed={canAccessHrOperations(roles)}><HrDrawHistoryPage /></Guard>} />
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
        <Route path="/legal" element={<LegalPage />} />
        <Route path="/*" element={<Shell />} />
      </Routes>
    </BrowserRouter>
  );
}
