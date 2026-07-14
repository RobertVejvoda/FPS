import { statusSolidColor } from './statusColors.js';

// LOC001 (#744): `label` lets the host app pass localized display text while
// `status` keeps driving the color mapping from the stable machine value.
export function StatusBadge({ status, label }: { status: string; label?: string }) {
  const color = statusSolidColor(status);
  return (
    <span style={{
      display: 'inline-block',
      padding: '2px 8px',
      borderRadius: 4,
      backgroundColor: color,
      color: '#fff',
      fontSize: 11,
      fontWeight: 600,
      letterSpacing: '0.3px',
    }}>
      {label ?? status}
    </span>
  );
}
