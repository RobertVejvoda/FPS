import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthProvider } from './auth/AuthContext';
import { LocaleProvider } from './i18n';
import { App } from './App';
import './styles.css';

const root = document.getElementById('root');
if (!root) throw new Error('Root element not found');

createRoot(root).render(
  <StrictMode>
    <AuthProvider>
      {/* LOC001 (#744) — inside AuthProvider so tenant/config defaults feed
          locale resolution; outside App so every route is localized. */}
      <LocaleProvider>
        <App />
      </LocaleProvider>
    </AuthProvider>
  </StrictMode>,
);
