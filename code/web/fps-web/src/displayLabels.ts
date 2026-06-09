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

// Draw lifecycle step labels (DRAW004)
const LIFECYCLE_STEP_LABELS: Record<string, string> = {
  Scheduled: 'Draw scheduled',
  DrawInputReady: 'Input data ready',
  DrawExecuted: 'Draw executed',
  DecisionsPersisted: 'Decisions saved',
  NotificationsSent: 'Notifications sent',
  DrawCompleted: 'Draw completed',
  DrawFailed: 'Draw failed',
  RecoveryInitiated: 'Recovery initiated',
};

export function formatLifecycleStepName(name: string): string {
  return LIFECYCLE_STEP_LABELS[name] ?? name;
}

const LIFECYCLE_STEP_STATUS_COLORS: Record<string, string> = {
  Completed: '#22c55e',
  Failed: '#ef4444',
  InProgress: '#2563eb',
  Pending: '#94a3b8',
};

export function lifecycleStepStatusColor(status: string): string {
  return LIFECYCLE_STEP_STATUS_COLORS[status] ?? '#94a3b8';
}

// Audit event type display labels (AUDIT003)
const AUDIT_EVENT_TYPE_LABELS: Record<string, string> = {
  'booking.requestSubmitted': 'Booking request submitted',
  'booking.requestRejected': 'Booking request rejected',
  'booking.slotAllocated': 'Parking slot allocated',
  'booking.requestCancelled': 'Booking request cancelled',
  'booking.usageConfirmed': 'Usage confirmed',
  'booking.noShowRecorded': 'No-show recorded',
  'booking.requestExpired': 'Booking request expired',
  'booking.drawStarted': 'Draw started',
  'booking.drawCompleted': 'Draw completed',
  'booking.drawFailed': 'Draw failed',
  'booking.penaltyApplied': 'Penalty applied',
  'booking.manualCorrectionApplied': 'Manual correction applied',
  'privacy.erasureRequested': 'Privacy erasure requested',
  'privacy.erasureCompleted': 'Privacy erasure completed',
  'privacy.erasureRejected': 'Privacy erasure rejected',
  'privacy.erasureStepRecorded': 'Privacy erasure step recorded',
  'configuration.policyChanged': 'Policy configuration changed',
  'configuration.capacityChanged': 'Capacity configuration changed',
  'notification.deliveryChanged': 'Notification delivery status changed',
};

export function humanizeAuditEventType(eventType: string): string {
  return AUDIT_EVENT_TYPE_LABELS[eventType] ?? eventType;
}

// Audit action display labels (AUDIT003)
export function humanizeAuditAction(action: string): string {
  return action.charAt(0).toUpperCase() + action.slice(1).replace(/([A-Z])/g, ' $1').trim();
}

// Audit result display labels (AUDIT003)
const AUDIT_RESULT_LABELS: Record<string, string> = {
  accepted: 'Accepted',
  rejected: 'Rejected',
  allocated: 'Allocated',
  cancelled: 'Cancelled',
  started: 'Started',
  completed: 'Completed',
  failed: 'Failed',
  recorded: 'Recorded',
  applied: 'Applied',
  updated: 'Updated',
  confirmed: 'Confirmed',
  expired: 'Expired',
};

export function humanizeAuditResult(result: string | null): string {
  if (!result) return '—';
  return AUDIT_RESULT_LABELS[result] ?? result;
}

// Activity category display labels (AUDIT003)
export function humanizeActivityCategory(category: string): string {
  const labels: Record<string, string> = {
    All: 'All activity',
    BookingLifecycle: 'Booking lifecycle',
    DrawEvents: 'Draw events',
    PolicyChanges: 'Policy & configuration',
    Notifications: 'Notifications',
    PrivacyErasure: 'Privacy & erasure',
    ManualCorrections: 'Manual corrections',
  };
  return labels[category] ?? category;
}

// Actor type display labels (AUDIT003)
export function humanizeActorType(actorType: string): string {
  const labels: Record<string, string> = {
    employee: 'Employee',
    hr: 'HR Manager',
    admin: 'Administrator',
    system: 'System',
    integration: 'Integration',
  };
  return labels[actorType] ?? actorType;
}

// Entity type display labels (AUDIT003)
export function humanizeEntityType(entityType: string): string {
  const labels: Record<string, string> = {
    bookingRequest: 'Booking request',
    drawAttempt: 'Draw attempt',
    policy: 'Policy',
    capacity: 'Capacity',
    notification: 'Notification',
    erasureRequest: 'Erasure request',
    allocation: 'Allocation',
    penalty: 'Penalty',
    profile: 'Profile',
    tenant: 'Tenant',
  };
  return labels[entityType] ?? entityType;
}
