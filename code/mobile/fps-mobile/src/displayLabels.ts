const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';

const LOCATION_LABELS: Record<string, string> = {
  'LOC-MAIN': 'Main office',
};

const FACILITY_LABELS: Record<string, string> = {
  [DEMO_FACILITY_ID]: 'Main building',
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
  return isGuid(value) ? 'Assigned space' : value.replace(/^LOC-MAIN-/, 'Space ');
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
  PastDate: 'Cannot submit a request for a date in the past.',
  CutOffPassed: 'Requests for this time slot are closed.',
  DailyCapExceeded: 'The daily request cap for this date has been reached.',
  DuplicateRequest: 'You already have a request for an overlapping time slot.',
  VehicleConstraintUnmatched: 'The requested vehicle is not registered or is inactive in your profile.',
  NoCapacityAvailable: 'No matching parking slot is available.',
  NoCapacityForSameDay: 'No matching parking slot is available for your same-day request.',
  RequestorIneligible: 'You are not eligible for parking under current policy.',
  SameDayBookingDisabled: 'Same-day booking is not available for this location.',
  ProfileUnavailable: 'Profile data is unavailable. Please try again later.',
  DrawNotSelected: 'Not selected in draw.',
};

export function displayRejectionReason(reasonCode?: string | null, fallbackText?: string | null): string {
  if (reasonCode && REJECTION_CODE_LABELS[reasonCode]) {
    return REJECTION_CODE_LABELS[reasonCode];
  }
  if (fallbackText) {
    return fallbackText;
  }
  return 'This request was not eligible for allocation. Details are not available yet.';
}
