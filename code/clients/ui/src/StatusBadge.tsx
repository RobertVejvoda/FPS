const COLOR: Record<string, string> = {
  Submitted: '#1d4ed8',
  Pending: '#1d4ed8',
  Allocated: '#15803d',
  Rejected: '#b91c1c',
  Cancelled: '#6b7280',
  Expired: '#6b7280',
  Waitlisted: '#92400e',
  UsageConfirmed: '#15803d',
  NoShow: '#b45309',
};

// LOC001 (#744): `label` lets the host app pass localized display text while
// `status` keeps driving the color mapping from the stable machine value.
export function StatusBadge({ status, label }: { status: string; label?: string }) {
  const color = COLOR[status] ?? '#6b7280';
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
