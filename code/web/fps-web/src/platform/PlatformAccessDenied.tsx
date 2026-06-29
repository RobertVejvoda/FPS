import { useAuth } from '../auth/AuthContext';
import { formatRoles } from '../auth/roles';

// Shown when an authenticated, non-platform identity reaches the operator console.
// A tenant/customer token (any tenant role, even admin) is never a platform identity:
// the backend strips platform_* from customer tokens, so these users only ever hold
// tenant roles. We fail honest with a clear access-denied state rather than redirecting
// silently — the console is reachable only with a platform-plane token.
export function PlatformAccessDenied() {
  const { roles, logout } = useAuth();

  return (
    <div className="platform-shell app-shell">
      <main className="app-main">
        <div className="plat-access-denied">
          <h1>Platform console — access denied</h1>
          <p>
            This is the FairSpot <strong>operator</strong> console. It is reachable only with a
            platform-plane identity (<code>platform_admin</code>, <code>platform_operator</code>, or{' '}
            <code>platform_auditor</code>).
          </p>
          <p className="plat-muted">
            You are signed in as <strong>{formatRoles(roles)}</strong>, a tenant identity. Tenant
            accounts — including tenant administrators — cannot access the platform plane.
          </p>
          <button className="btn-danger" onClick={() => { void logout(); }}>Sign out</button>
        </div>
      </main>
    </div>
  );
}
