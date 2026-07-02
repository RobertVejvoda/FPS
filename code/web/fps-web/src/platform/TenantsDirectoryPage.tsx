import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchPlatformTenants, type PlatformTenantRow } from '../api/platform';

type State =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ok'; rows: PlatformTenantRow[] };

// PLAT008B — cross-tenant platform tenant directory (read-only). Lifecycle state, region, and
// timestamps are live from the Customer API; modules, usage, health, and last activity are shown
// as explicit "Not wired yet" placeholders until their data sources land (PLAT007/PLAT005).
export function TenantsDirectoryPage() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [query, setQuery] = useState('');
  const [stateFilter, setStateFilter] = useState('');

  useEffect(() => {
    let active = true;
    setState({ kind: 'loading' });
    void fetchPlatformTenants({ apiBaseUrl, bearerToken }).then((r) => {
      if (!active) return;
      if (r.kind === 'ok') setState({ kind: 'ok', rows: r.data });
      else if (r.kind === 'unauthenticated') setState({ kind: 'error', message: 'Your platform session is not authorized.' });
      else if (r.kind === 'unreachable') setState({ kind: 'error', message: 'Could not reach the platform API.' });
      else setState({ kind: 'error', message: r.message });
    });
    return () => { active = false; };
  }, [apiBaseUrl, bearerToken]);

  const states = useMemo(
    () => (state.kind === 'ok' ? Array.from(new Set(state.rows.map((r) => r.lifecycleState))).sort() : []),
    [state],
  );

  const filtered = useMemo(() => {
    if (state.kind !== 'ok') return [];
    const q = query.trim().toLowerCase();
    return state.rows.filter((r) => {
      const matchesQuery = !q || r.displayName.toLowerCase().includes(q) || r.slug.toLowerCase().includes(q) || r.region.toLowerCase().includes(q);
      const matchesState = !stateFilter || r.lifecycleState === stateFilter;
      return matchesQuery && matchesState;
    });
  }, [state, query, stateFilter]);

  return (
    <section className="plat-page">
      <header className="plat-page-head">
        <h1>Tenant directory</h1>
        <p className="plat-muted">Cross-tenant, read-only. State, region, timestamps, and modules are live; usage, health, and last activity are not wired yet.</p>
      </header>

      <div className="plat-filters">
        <input
          className="plat-input"
          placeholder="Search name, slug, or region…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          aria-label="Search tenants"
        />
        <select className="plat-input" value={stateFilter} onChange={(e) => setStateFilter(e.target.value)} aria-label="Filter by state">
          <option value="">All states</option>
          {states.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {state.kind === 'loading' && <p className="plat-muted">Loading tenants…</p>}
      {state.kind === 'error' && <p className="plat-error" role="alert">{state.message}</p>}
      {state.kind === 'ok' && filtered.length === 0 && (
        <p className="plat-muted">{state.rows.length === 0 ? 'No tenants yet.' : 'No tenants match your filter.'}</p>
      )}

      {state.kind === 'ok' && filtered.length > 0 && (
        <table className="plat-table">
          <thead>
            <tr>
              <th>Tenant</th><th>State</th><th>Region</th><th>Modules</th><th>Usage</th><th>Health</th><th>Last activity</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((r) => (
              <tr key={r.tenantId}>
                <td><Link to={`/platform/tenants/${encodeURIComponent(r.tenantId)}`}>{r.displayName}</Link><div className="plat-muted plat-sub">{r.slug}</div></td>
                <td>{r.lifecycleState}</td>
                <td>{r.region || '—'}</td>
                <td title={`Enabled: ${(r.enabledModules.length > 0 ? r.enabledModules : [r.primaryModule]).join(', ')}`}>{r.primaryModule}{r.enabledModules.length > 1 ? ` +${r.enabledModules.length - 1}` : ''}</td>
                <td className="plat-na" title="Usage ledger not wired yet (PLAT005)">Not wired yet</td>
                <td className="plat-na" title="Composite health not wired yet">Not wired yet</td>
                <td className="plat-na" title="Activity feed not wired yet (DataHub)">Not wired yet</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
