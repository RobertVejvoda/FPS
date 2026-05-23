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
  const { isConfigured, logout } = useAuth();

  if (!isConfigured) return <Navigate to="/session" replace />;

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
