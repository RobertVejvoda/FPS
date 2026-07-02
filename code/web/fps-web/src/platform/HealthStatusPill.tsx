import type { HealthStatus } from '../api/platform';

// PLAT008D — the health strip's status legend. Each pill is one of four honest states; the copy is
// business-readable and the colour matches the availability badges used elsewhere in the console.
const LABELS: Record<HealthStatus, string> = {
  ok: 'OK',
  warning: 'Warning',
  unavailable: 'Unavailable',
  'not-wired': 'Not wired yet',
};

export function HealthStatusPill({ status }: { status: HealthStatus }) {
  return <span className={`health-pill health-${status}`}>{LABELS[status]}</span>;
}
