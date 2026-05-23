import { BrowserRouter, Navigate, NavLink, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import {
  canAccessAudit,
  canAccessBookings,
  canAccessConfiguration,
  canAccessReporting,
  canAccessTenantAdmin,
} from './auth/roles';
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
import { ForbiddenPage } from './pages/ForbiddenPage';

function Guard({ allowed, children }: { allowed: boolean; children: React.ReactNode }) {
  return allowed ? <>{children}</> : <ForbiddenPage />;
}

function Shell() {
  const { isConfigured, logout, roles } = useAuth();

  if (!isConfigured) return <Navigate to="/session" replace />;

  const navItems = [
    canAccessBookings(roles) && { to: '/bookings', label: 'Bookings' },
    { to: '/profile', label: 'Profile' },
    { to: '/notifications', label: 'Notifications' },
    canAccessReporting(roles) && { to: '/reporting', label: 'Reports' },
    canAccessConfiguration(roles) && { to: '/configuration', label: 'Configuration' },
    canAccessAudit(roles) && { to: '/audit', label: 'Audit' },
    canAccessTenantAdmin(roles) && { to: '/tenant-admin', label: 'Admin' },
  ].filter(Boolean) as { to: string; label: string }[];

  return (
    <div style={{ minHeight: '100vh', background: '#f9fafb' }}>
      <header style={{ background: '#fff', borderBottom: '1px solid #e5e7eb', padding: '0 24px', height: 52, display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16 }}>
        <span style={{ fontWeight: 700, fontSize: 15, color: '#111827', flexShrink: 0 }}>FairSpot</span>
        <nav style={{ display: 'flex', gap: 2, overflowX: 'auto', flexShrink: 1 }}>
          {navItems.map(item => (
            <NavLink
              key={item.to}
              to={item.to}
              style={({ isActive }) => ({
                padding: '6px 12px',
                fontSize: 13,
                fontWeight: 500,
                textDecoration: 'none',
                borderRadius: 6,
                color: isActive ? '#1d4ed8' : '#6b7280',
                background: isActive ? '#eff6ff' : 'transparent',
                whiteSpace: 'nowrap',
              })}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <button
          onClick={() => { void logout(); }}
          style={{ background: 'none', border: 'none', color: '#b91c1c', fontSize: 13, cursor: 'pointer', fontWeight: 600, flexShrink: 0 }}
        >
          Sign out
        </button>
      </header>
      <main style={{ maxWidth: 720, margin: '0 auto', padding: '32px 24px' }}>
        <Routes>
          <Route path="/bookings" element={<Guard allowed={canAccessBookings(roles)}><BookingsPage /></Guard>} />
          <Route path="/bookings/new" element={<Guard allowed={canAccessBookings(roles)}><NewBookingPage /></Guard>} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/reporting" element={<Guard allowed={canAccessReporting(roles)}><ReportingPage /></Guard>} />
          <Route path="/configuration" element={<Guard allowed={canAccessConfiguration(roles)}><ConfigurationPage /></Guard>} />
          <Route path="/audit" element={<Guard allowed={canAccessAudit(roles)}><AuditPage /></Guard>} />
          <Route path="/tenant-admin" element={<Guard allowed={canAccessTenantAdmin(roles)}><TenantAdminPage /></Guard>} />
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
