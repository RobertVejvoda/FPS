import { Link } from 'react-router-dom';
import { useTenantModules } from './TenantModulesContext';

// PLAT-seats (#710) — a compact Parking | Seats switch for the employee allocation surface. Per the
// product rule it renders only when the tenant enables more than one module; a single-module tenant
// sees no switch and its experience is unchanged.
export function ModuleSwitch({ active }: { active: 'parking' | 'seats' }) {
  const { multiModule } = useTenantModules();
  if (!multiModule) return null;
  return (
    <div className="module-switch" role="tablist" aria-label="Allocation module">
      <Link to="/bookings" role="tab" aria-selected={active === 'parking'} className={`module-tab${active === 'parking' ? ' module-tab-active' : ''}`}>Parking</Link>
      <Link to="/seats" role="tab" aria-selected={active === 'seats'} className={`module-tab${active === 'seats' ? ' module-tab-active' : ''}`}>Seats</Link>
    </div>
  );
}
