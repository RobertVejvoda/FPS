const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';

const LOCATION_LABELS: Record<string, string> = {
  Prague: 'Prague',
};

const FACILITY_LABELS: Record<string, string> = {
  [DEMO_FACILITY_ID]: 'Headquarters',
};

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

export function displayLocation(value?: string | null): string | null {
  if (!value) return null;
  return LOCATION_LABELS[value] ?? FACILITY_LABELS[value] ?? (isGuid(value) ? 'Selected location' : value);
}

export function displaySlot(value?: string | null): string | null {
  if (!value) return null;
  return isGuid(value) ? 'Assigned space' : value.replace(/^Prague-/, 'Space ');
}

function formatDisplayTime(hour: number, minute: number): string {
  return `${hour % 12 || 12}:${String(minute).padStart(2, '0')} ${hour >= 12 ? 'PM' : 'AM'}`;
}

export function displayNextDrawRun(requestedDate?: string | null, cutOffTime = '18:00'): string | null {
  if (!requestedDate) return null;
  const [year, month, day] = requestedDate.split('-').map(Number);
  const [hour, minute] = cutOffTime.split(':').map(Number);
  if (!year || !month || !day || Number.isNaN(hour) || Number.isNaN(minute)) return null;

  const drawDate = new Date(year, month - 1, day);
  drawDate.setDate(drawDate.getDate() - 1);
  const dateLabel = drawDate.toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  });
  return `${dateLabel}, ${formatDisplayTime(hour, minute)}`;
}

export function shouldShowNextDraw(status?: string | null): boolean {
  return status === 'Pending' || status === 'Submitted';
}

export function formatCutOffAt(cutOffAt: string | null, timeZone: string): string {
  if (!cutOffAt) return '—';
  try {
    return new Date(cutOffAt).toLocaleTimeString(undefined, {
      hour: '2-digit',
      minute: '2-digit',
      timeZone,
      timeZoneName: 'short',
    });
  } catch {
    return cutOffAt;
  }
}

const REJECTION_CODE_LABELS: Record<string, string> = {
  PolicyCutoff: 'Booking deadline has passed for this date.',
  IneligibleProfile: 'Your profile was not eligible for this allocation.',
  MissingVehicleEligibility: 'Vehicle eligibility requirement was not met.',
  NoMatchingCapacity: 'No available spaces matched this request.',
  DrawNotSelected: 'Your request was not selected in this allocation draw.',
};

export function humanizeRejectionReason(reasonCode: string | null, reason: string | null): string {
  if (reason) return reason;
  if (reasonCode) return REJECTION_CODE_LABELS[reasonCode] ?? 'This request was not eligible for allocation.';
  return 'This request was not eligible for allocation.';
}

const SCHEDULE_STATUS_LABELS: Record<string, string> = {
  known: 'Schedule configured',
  unknown: 'Not configured',
  pending: 'Pending',
  completed: 'Completed',
  running: 'Running',
};

const SCHEDULE_SOURCE_LABELS: Record<string, string> = {
  tenantPolicy: 'Tenant policy',
  locationOverride: 'Location override',
  manualOnly: 'Manual only',
  default: 'Default',
};

export function formatScheduleStatus(status: string): string {
  return SCHEDULE_STATUS_LABELS[status] ?? status;
}

export function formatScheduleSource(source: string): string {
  return SCHEDULE_SOURCE_LABELS[source] ?? source;
}

export function formatDrawTimestamp(at: string | null, timeZone: string): string {
  if (!at) return '—';
  try {
    return new Date(at).toLocaleString(undefined, {
      weekday: 'short', month: 'short', day: 'numeric',
      hour: '2-digit', minute: '2-digit',
      timeZone, timeZoneName: 'short',
    });
  } catch {
    return at;
  }
}

export function isTimestampInPast(isoTimestamp: string | null): boolean {
  if (!isoTimestamp) return false;
  return new Date(isoTimestamp) < new Date();
}

const HR_REJECTION_LABELS: Record<string, string> = {
  PolicyCutoff: 'Cut-off deadline passed',
  IneligibleProfile: 'Profile ineligible',
  MissingVehicleEligibility: 'Vehicle eligibility not met',
  NoMatchingCapacity: 'No matching capacity',
  DrawNotSelected: 'Not selected in draw',
};

export function humanizeHrRejection(reasonCode: string | null, reason: string | null): string {
  if (reason) return reason;
  if (reasonCode) return HR_REJECTION_LABELS[reasonCode] ?? reasonCode;
  return '—';
}

export function getWeekdayName(dateString: string): string {
  const date = new Date(dateString + 'T00:00:00');
  return date.toLocaleDateString(undefined, { weekday: 'long' });
}
