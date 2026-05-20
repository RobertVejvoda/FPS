import { BrowserRouter, Navigate, Route, Routes, useNavigate } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { SessionPage } from './pages/SessionPage';
import { BookingsPage } from './pages/BookingsPage';
import { NewBookingPage } from './pages/NewBookingPage';

function Shell() {
  const { isConfigured, clear } = useAuth();
  const navigate = useNavigate();

  if (!isConfigured) return <Navigate to="/session" replace />;

  return (
    <div style={{ minHeight: '100vh', background: '#f9fafb' }}>
      <header style={{ background: '#fff', borderBottom: '1px solid #e5e7eb', padding: '0 24px', height: 52, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontWeight: 700, fontSize: 16, color: '#111827' }}>FPS Employee Portal</span>
        <button
          onClick={() => { clear(); navigate('/session'); }}
          style={{ background: 'none', border: 'none', color: '#b91c1c', fontSize: 13, cursor: 'pointer', fontWeight: 600 }}
        >
          Sign out
        </button>
      </header>
      <main style={{ maxWidth: 720, margin: '0 auto', padding: '32px 24px' }}>
        <Routes>
          <Route path="/bookings" element={<BookingsPage />} />
          <Route path="/bookings/new" element={<NewBookingPage />} />
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
        <Route path="/*" element={<Shell />} />
      </Routes>
    </BrowserRouter>
  );
}
