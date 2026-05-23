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

export function StatusBadge({ status }: { status: string }) {
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
      {status}
    </span>
  );
}
