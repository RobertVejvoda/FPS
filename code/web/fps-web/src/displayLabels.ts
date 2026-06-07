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

const ROLE_LABELS: Record<string, string> = {
  // PascalCase forms (identity token claims)
  EmployeeMobile: 'Employee',
  Employee: 'Employee',
  Admin: 'Administrator',
  Auditor: 'Auditor',
  HrManager: 'HR Manager',
  ReportViewer: 'Report Viewer',
  // snake_case forms (backend role values)
  employee: 'Employee',
  admin: 'Administrator',
  auditor: 'Auditor',
  hr_manager: 'HR Manager',
  report_viewer: 'Report Viewer',
};

export function formatRoles(roles: string[]): string {
  const seen = new Set<string>();
  const labels: string[] = [];
  for (const r of roles) {
    const label = ROLE_LABELS[r];
    if (label && !seen.has(label)) {
      seen.add(label);
      labels.push(label);
    }
  }
  return labels.join(', ') || 'Employee';
}
