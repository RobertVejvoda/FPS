// UXPOL001 (#798): single source of truth for booking-status colors so web
// surfaces stop drifting apart. Two render styles share one semantic mapping:
// - solid: white text on a strong background (compact badges)
// - soft: tinted background with dark text (list chips, tables)
// Semantics: blue = in flight, green = positive outcome, red = rejected,
// gray = neutral/terminal, amber = waitlisted, orange = no-show/penalty.

export interface StatusTone {
  background: string;
  color: string;
  border: string;
}

export const STATUS_SOLID: Record<string, string> = {
  Submitted: '#1d4ed8',
  Pending: '#1d4ed8',
  Allocated: '#15803d',
  Rejected: '#b91c1c',
  Cancelled: '#6b7280',
  Expired: '#6b7280',
  Waitlisted: '#92400e',
  UsageConfirmed: '#15803d',
  Used: '#15803d',
  NoShow: '#b45309',
};

const SOLID_FALLBACK = '#6b7280';

const BLUE_SOFT: StatusTone = { background: '#eff6ff', color: '#1d4ed8', border: '#bfdbfe' };
const GREEN_SOFT: StatusTone = { background: '#f0fdf4', color: '#166534', border: '#bbf7d0' };
const RED_SOFT: StatusTone = { background: '#fef2f2', color: '#991b1b', border: '#fecaca' };
const GRAY_SOFT: StatusTone = { background: '#f9fafb', color: '#6b7280', border: '#e5e7eb' };
const AMBER_SOFT: StatusTone = { background: '#fffbeb', color: '#92400e', border: '#fcd34d' };
const ORANGE_SOFT: StatusTone = { background: '#fff7ed', color: '#9a3412', border: '#fed7aa' };

export const STATUS_SOFT: Record<string, StatusTone> = {
  Submitted: BLUE_SOFT,
  Pending: BLUE_SOFT,
  Allocated: GREEN_SOFT,
  UsageConfirmed: GREEN_SOFT,
  Used: GREEN_SOFT,
  Rejected: RED_SOFT,
  Cancelled: GRAY_SOFT,
  Expired: GRAY_SOFT,
  Waitlisted: AMBER_SOFT,
  NoShow: ORANGE_SOFT,
};

export function statusSolidColor(status: string): string {
  return STATUS_SOLID[status] ?? SOLID_FALLBACK;
}

export function statusSoftTone(status: string): StatusTone {
  return STATUS_SOFT[status] ?? GRAY_SOFT;
}
