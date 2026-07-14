// LOC001 (#744) — all user-visible labels resolve through the i18n catalog
// (src/i18n/messages/labels.ts); machine codes stay internal and reach the
// catalog only as lookup keys. Function signatures are unchanged from the
// pre-localization helpers so call sites stay as they were.
import { t, tDynamic, tPlural, formatDate, formatDateTime, formatTime, formatWallClock } from './i18n';

const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';

// Location/facility ids with an employee-safe display name in the catalog.
const KNOWN_LOCATION_IDS = new Set(['Prague', 'GL-HQ', 'GL-TEAMS']);

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

export function displayLocation(value?: string | null): string | null {
  if (!value) return null;
  // UX009 review (#790) — employee screens must not show raw location ids.
  if (KNOWN_LOCATION_IDS.has(value)) return tDynamic('labels.location', value, value);
  if (value === DEMO_FACILITY_ID) return t('labels.location.GL-HQ');
  return isGuid(value) ? t('labels.location.selected') : value;
}

export function displaySlot(value?: string | null): string | null {
  if (!value) return null;
  if (isGuid(value)) return t('labels.slot.assigned');
  if (value.startsWith('Prague-')) return t('labels.slot.spacePrefix', { id: value.slice('Prague-'.length) });
  return value;
}

export function displayRequestorRef(value?: string | null): string {
  if (!value) return t('labels.requestor');

  const compact = value.replace(/-/g, '');
  if (/^[0-9a-f]{32,}$/i.test(compact)) {
    return t('labels.requestor.withRef', { ref: compact.slice(0, 6).toUpperCase() });
  }

  return value.length > 18 ? `${value.slice(0, 18)}...` : value;
}

// Short, label-free form of a requestor ref — just the 6-char support id with
// no "Requestor" prefix. Useful when the caller wants to compose its own
// explicit fallback (e.g. "Unknown requestor · 585624" on the Reports surface,
// per #480 acceptance criteria) without the prefix getting in the way.
export function shortRequestorRef(value?: string | null): string {
  if (!value) return '';

  const compact = value.replace(/-/g, '');
  if (/^[0-9a-f]{32,}$/i.test(compact)) {
    return compact.slice(0, 6).toUpperCase();
  }

  return value.length > 18 ? `${value.slice(0, 18)}...` : value;
}

// UX008 (#781) — employee-safe module labels for the module-aware My Reservations
// surface. Booking items default to Parking; anything else falls back to the raw
// resource type so a future module still renders a readable badge.
export function isSeatsItem(item: { resourceType?: string | null }): boolean {
  return item.resourceType === 'Seats';
}

export function displayModule(resourceType?: string | null): string {
  if (!resourceType || resourceType === 'Parking') return t('labels.module.Parking');
  return tDynamic('labels.module', resourceType, resourceType);
}

// The employee-facing name of the allocated resource: parking uses "Spot",
// seats use "Seat". Keeps row/detail copy business-readable per module.
export function displayResourceNoun(resourceType?: string | null): string {
  return resourceType === 'Seats' ? t('labels.resourceNoun.seat') : t('labels.resourceNoun.spot');
}

export function displayDate(value?: string | null): string {
  if (!value) return '-';
  try {
    return formatDate(new Date(`${value}T00:00:00`));
  } catch {
    return value;
  }
}

export function displayDateTime(value?: string | null): string {
  if (!value) return '-';
  try {
    return formatDateTime(new Date(value));
  } catch {
    return value;
  }
}

export function displayNextDrawRun(requestedDate?: string | null, cutOffTime = '18:00'): string | null {
  if (!requestedDate) return null;
  const [year, month, day] = requestedDate.split('-').map(Number);
  const [hour, minute] = cutOffTime.split(':').map(Number);
  if (!year || !month || !day || Number.isNaN(hour) || Number.isNaN(minute)) return null;

  const drawDate = new Date(year, month - 1, day);
  drawDate.setDate(drawDate.getDate() - 1);
  const dateLabel = formatDate(drawDate, { weekday: 'short', month: 'short', day: 'numeric' });
  return `${dateLabel}, ${formatWallClock(hour, minute)}`;
}

export function shouldShowNextDraw(status?: string | null): boolean {
  return status === 'Pending' || status === 'Submitted';
}

export function formatCutOffAt(cutOffAt: string | null, timeZone: string): string {
  if (!cutOffAt) return '—';
  try {
    return formatTime(new Date(cutOffAt), {
      hour: '2-digit',
      minute: '2-digit',
      timeZone,
      timeZoneName: 'short',
    });
  } catch {
    return cutOffAt;
  }
}

// Booking status pills — stable machine value in, localized display text out.
export function displayBookingStatus(status: string): string {
  return tDynamic('bookings.status', status, status);
}

// The draw-status API ships structured state (draw status, request-window
// status, schedule status, cut-off) alongside an English safe-message. Derive
// localized copy from the structured fields and keep the server text as the
// fallback for combinations the client doesn't recognize. Stable message
// codes on the API are a LOC001 follow-up.
type ScheduleLike = {
  status?: string;
  requestWindowStatus?: string;
  scheduleStatus?: string;
  cutOffAt?: string | null;
  timeZone?: string;
  safeMessage?: string;
  cannotRequestReason?: string | null;
};

export function displayScheduleMessage(s: ScheduleLike): string | null {
  // Wire values: draw status is PascalCase ("Completed"), while the schedule
  // metadata enums serialize camelCase ("closed", "known", "notConfigured").
  if (s.status === 'Completed') return t('labels.schedule.allocationComplete');
  if (s.scheduleStatus === 'notConfigured') return t('labels.schedule.notConfigured');
  if (s.requestWindowStatus === 'closed') return t('labels.schedule.windowClosed');
  if (s.requestWindowStatus === 'open' && s.cutOffAt && s.timeZone) {
    return t('labels.schedule.openUntil', { time: formatCutOffAt(s.cutOffAt, s.timeZone) });
  }
  return s.safeMessage || null;
}

export function displayCannotRequestReason(s: ScheduleLike): string | null {
  if (!s.cannotRequestReason) return null;
  if (s.status === 'Completed') return t('labels.schedule.allocationCompleteShort');
  if (s.status === 'InProgress') return t('labels.schedule.drawInProgress');
  if (s.cannotRequestReason === 'Date has passed') return t('labels.schedule.datePassed');
  if (s.requestWindowStatus === 'closed') return displayScheduleMessage(s);
  return s.cannotRequestReason;
}

export function humanizeRejectionReason(reasonCode: string | null, reason: string | null): string {
  // A stable code localizes; the backend's free-text reason is the fallback
  // for codes the catalog doesn't know yet.
  if (reasonCode) return tDynamic('labels.rejection', reasonCode, reason ?? t('labels.rejection.fallback'));
  if (reason) return reason;
  return t('labels.rejection.fallback');
}

export function formatScheduleStatus(status: string): string {
  return tDynamic('labels.scheduleStatus', status, status);
}

export function formatScheduleSource(source: string): string {
  return tDynamic('labels.scheduleSource', source, source);
}

export function formatDrawStatus(status: string): string {
  return tDynamic('labels.drawStatus', status, status);
}

export function formatDemandLevel(demandLevel: string): string {
  return tDynamic('labels.demand', demandLevel, demandLevel);
}

export function formatDrawRequestSummary(requestCount: number, demandLevel: string): string {
  if (requestCount === 0 && demandLevel === 'Unknown') {
    return t('labels.drawRequestSummary.none');
  }

  return `${tPlural('labels.drawRequestCount', requestCount)}. ${formatDemandLevel(demandLevel)}.`;
}

export function formatScheduleSummary(status: string, source: string): string {
  if (status === 'known' && source === 'tenantPolicy') return t('labels.scheduleSummary.tenantPolicy');
  if (status === 'known' && source === 'locationOverride') return t('labels.scheduleSummary.locationOverride');
  if (status === 'known' && source === 'manualOnly') return t('labels.scheduleSummary.manualOnly');
  if (status === 'known') return t('labels.scheduleSummary.configured');
  if (status === 'unknown') return t('labels.scheduleSummary.none');
  return formatScheduleStatus(status);
}

export function formatDrawTimestamp(at: string | null, timeZone: string): string {
  if (!at) return '—';
  try {
    return formatDateTime(new Date(at), {
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

export function humanizeHrRejection(reasonCode: string | null, reason: string | null): string {
  if (reasonCode) return tDynamic('labels.hrRejection', reasonCode, reason ?? reasonCode);
  if (reason) return reason;
  return '—';
}

// Draw lifecycle step labels (DRAW004/DRAW009).
// Names match the StepName values emitted by Booking workflow activities.
export function formatLifecycleStepName(name: string): string {
  return tDynamic('labels.lifecycleStep', name, name);
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
export function humanizeAuditEventType(eventType: string): string {
  return tDynamic('labels.auditEvent', eventType, eventType);
}

// Audit action display labels (AUDIT003). Unknown actions fall back to a
// camelCase split; known values arrive as audit event types and results.
export function humanizeAuditAction(action: string): string {
  return action.charAt(0).toUpperCase() + action.slice(1).replace(/([A-Z])/g, ' $1').trim();
}

// Audit result display labels (AUDIT003)
export function humanizeAuditResult(result: string | null): string {
  if (!result) return '—';
  return tDynamic('labels.auditResult', result, result);
}

// Activity category display labels (AUDIT003)
export function humanizeActivityCategory(category: string): string {
  return tDynamic('labels.activityCategory', category, category);
}

export function displayActorRef(hash: string | null): string {
  if (!hash) return '—';
  const compact = hash.replace(/-/g, '');
  if (/^[0-9a-f]{32,}$/i.test(compact)) return compact.slice(0, 6).toUpperCase();
  return hash.length > 20 ? `${hash.slice(0, 20)}…` : hash;
}

// Actor type display labels (AUDIT003). HR cancellation and HR operations
// emit actorType=hr_manager (#482 review); the older 'hr' value is kept for
// backwards compatibility with any historical audit rows in the store.
export function humanizeActorType(actorType: string): string {
  return tDynamic('labels.actorType', actorType, actorType);
}

// Entity type display labels (AUDIT003)
export function humanizeEntityType(entityType: string): string {
  return tDynamic('labels.entityType', entityType, entityType);
}
