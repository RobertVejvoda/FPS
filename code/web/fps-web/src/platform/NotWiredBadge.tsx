// Availability legend from the dashboard design (platform-dashboard-ux.md §9). A source that
// is not implemented renders an explicit "Not wired yet" badge — never a fake operational
// value. `slice` names the backing slice that will wire it, so reviewers see the build order.
type Availability = 'not-wired' | 'partial' | 'live';

const LABELS: Record<Availability, string> = {
  'not-wired': 'Not wired yet',
  partial: 'Partial',
  live: 'Live',
};

export function NotWiredBadge({ availability = 'not-wired', slice }: { availability?: Availability; slice?: string }) {
  return (
    <span className={`avail-badge avail-${availability}`}>
      {LABELS[availability]}
      {slice ? <span className="avail-slice"> · {slice}</span> : null}
    </span>
  );
}
