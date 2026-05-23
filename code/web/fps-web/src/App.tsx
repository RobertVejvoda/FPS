import { BrowserRouter, Navigate, NavLink, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { SessionPage } from './pages/SessionPage';
import { OidcCallbackPage } from './pages/OidcCallbackPage';
import { BookingsPage } from './pages/BookingsPage';
import { NewBookingPage } from './pages/NewBookingPage';
import { ProfilePage } from './pages/ProfilePage';
import { NotificationsPage } from './pages/NotificationsPage';
import { ReportingPage } from './pages/ReportingPage';
import { ConfigurationPage } from './pages/ConfigurationPage';
import { AuditPage } from './pages/AuditPage';
import { TenantAdminPage } from './pages/TenantAdminPage';

const navItems = [
  { to: '/bookings', label: 'Bookings' },
  { to: '/profile', label: 'Profile' },
  { to: '/notifications', label: 'Notifications' },
  { to: '/reporting', label: 'Reports' },
  { to: '/configuration', label: 'Configuration' },
  { to: '/audit', label: 'Audit' },
  { to: '/tenant-admin', label: 'Admin' },
];

function Shell() {
  const { isConfigured, logout, branding } = useAuth();

  if (!isConfigured) return <Navigate to="/session" replace />;

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
          <Route path="/bookings" element={<BookingsPage />} />
          <Route path="/bookings/new" element={<NewBookingPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/reporting" element={<ReportingPage />} />
          <Route path="/configuration" element={<ConfigurationPage />} />
          <Route path="/audit" element={<AuditPage />} />
          <Route path="/tenant-admin" element={<TenantAdminPage />} />
          <Route path="*" element={<Navigate to="/bookings" replace />} />
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
        <Route path="/*" element={<Shell />} />
      </Routes>
    </BrowserRouter>
  );
}
