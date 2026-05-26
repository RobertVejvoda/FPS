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
  PolicyCutoff: 'Booking deadline has passed for this date.',
  IneligibleProfile: 'Your profile was not eligible for this allocation.',
  MissingVehicleEligibility: 'Vehicle eligibility requirement was not met.',
  NoMatchingCapacity: 'No available spaces matched this request.',
  DrawOutcome: 'Your request was not selected in this allocation draw.',
};

export function humanizeRejectionReason(rejectionCode: string | null, reason: string | null): string {
  if (reason) return reason;
  if (rejectionCode) {
    return REJECTION_CODE_LABELS[rejectionCode] ?? 'This request was not eligible for allocation. Details are not available yet.';
  }
  return 'This request was not eligible for allocation. Details are not available yet.';
}

export function formatBookingRef(requestId: string, requestedDate?: string): string {
  const datePart = requestedDate
    ? requestedDate.replace(/-/g, '')
    : new Date().toISOString().slice(0, 10).replace(/-/g, '');
  const shortCode = requestId.replace(/-/g, '').slice(-4).toUpperCase();
  return `BK-${datePart}-${shortCode}`;
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
    const label = ROLE_LABELS[r] ?? r;
    if (!seen.has(label)) {
      seen.add(label);
      labels.push(label);
    }
  }
  return labels.join(', ') || 'Employee';
}

export const STATUS_BADGE_LABEL: Record<string, string> = {
  Submitted: 'Submitted',
  Pending: 'Pending',
  Allocated: 'Allocated',
  Rejected: 'Not Allocated',
  Cancelled: 'Cancelled',
  Expired: 'Expired',
  Waitlisted: 'Waitlisted',
  UsageConfirmed: 'Confirmed',
  NoShow: 'No Show',
};

const DEMAND_EXPLANATIONS: Record<string, string> = {
  Low: 'Demand is low — most requests are typically fulfilled.',
  Medium: 'Demand is moderate — some requests may not receive a space.',
  High: 'Demand is high — spaces are limited. Final allocation follows eligibility and fairness rules.',
  Unknown: 'Demand information is not yet available for this date.',
};

export function displayDemandLevel(level: string | null | undefined): { label: string; explanation: string } | null {
  if (!level || level === 'Unknown') return null;
  return {
    label: `Demand: ${level}`,
    explanation: DEMAND_EXPLANATIONS[level] ?? DEMAND_EXPLANATIONS['Unknown'],
  };
}
