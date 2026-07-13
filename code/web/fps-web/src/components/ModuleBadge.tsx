import { displayModule } from '../displayLabels';

// UX008 (#781) — compact module badge for the module-aware My Reservations
// surface. Rendered only when the tenant runs (or the employee's records span)
// more than one module, so a parking-only tenant sees no module concepts.
export function ModuleBadge({ resourceType }: { resourceType?: string | null }) {
  const label = displayModule(resourceType);
  const seats = resourceType === 'Seats';
  return (
    <span className={`module-badge${seats ? ' module-badge-seats' : ''}`}>{label}</span>
  );
}
