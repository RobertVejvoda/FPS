import { BrowserRouter, Navigate, NavLink, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import {
  canAccessAudit,
  canAccessBookings,
  canAccessConfiguration,
  canAccessNotifications,
  canAccessProfile,
  canAccessReporting,
  canAccessTenantAdmin,
  defaultRoute,
} from './auth/roles';
import { SessionPage } from './pages/SessionPage';
import { OidcCallbackPage } from './pages/OidcCallbackPage';
import { BookingsPage } from './pages/BookingsPage';
import { BookingDetailPage } from './pages/BookingDetailPage';
import { NewBookingPage } from './pages/NewBookingPage';
import { ProfilePage } from './pages/ProfilePage';
import { NotificationsPage } from './pages/NotificationsPage';
import { ReportingPage } from './pages/ReportingPage';
import { ConfigurationPage } from './pages/ConfigurationPage';
import { AuditPage } from './pages/AuditPage';
import { TenantAdminPage } from './pages/TenantAdminPage';
import { HrImportPage } from './pages/HrImportPage';
import { ForbiddenPage } from './pages/ForbiddenPage';
import { LegalPage } from './pages/LegalPage';

function Guard({ allowed, children }: { allowed: boolean; children: React.ReactNode }) {
  return allowed ? <>{children}</> : <ForbiddenPage />;
}

function Shell() {
  const { isConfigured, logout, branding, roles } = useAuth();

  if (!isConfigured) return <Navigate to="/session" replace />;

  const navItems = [
    canAccessBookings(roles) && { to: '/bookings', label: 'My Spots' },
    canAccessProfile(roles) && { to: '/profile', label: 'Profile' },
    canAccessNotifications(roles) && { to: '/notifications', label: 'Notifications' },
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
          <Route path="/bookings/new" element={<Guard allowed={canAccessBookings(roles)}><NewBookingPage /></Guard>} />
          <Route path="/bookings/:requestId" element={<Guard allowed={canAccessBookings(roles)}><BookingDetailPage /></Guard>} />
          <Route path="/profile" element={<Guard allowed={canAccessProfile(roles)}><ProfilePage /></Guard>} />
          <Route path="/notifications" element={<Guard allowed={canAccessNotifications(roles)}><NotificationsPage /></Guard>} />
          <Route path="/reporting" element={<Guard allowed={canAccessReporting(roles)}><ReportingPage /></Guard>} />
          <Route path="/configuration" element={<Guard allowed={canAccessConfiguration(roles)}><ConfigurationPage /></Guard>} />
          <Route path="/hr-import" element={<Guard allowed={canAccessConfiguration(roles)}><HrImportPage /></Guard>} />
          <Route path="/audit" element={<Guard allowed={canAccessAudit(roles)}><AuditPage /></Guard>} />
          <Route path="/tenant-admin" element={<Guard allowed={canAccessTenantAdmin(roles)}><TenantAdminPage /></Guard>} />
          <Route path="*" element={<Navigate to={defaultRoute(roles)} replace />} />
        </Routes>
      </main>
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
