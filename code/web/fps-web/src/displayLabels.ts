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
