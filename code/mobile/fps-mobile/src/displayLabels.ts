// LOC001 (#744) — display-label helpers converted to the i18n catalog.
// Stable machine codes (location ids, rejection codes, statuses, roles) stay
// untranslated internally; only the label looked up via tDynamic()/t() is
// locale-aware. See src/i18n/messages/booking.ts and messages/more.ts for
// the `labels.*` catalog entries this file looks up.
import { t, tDynamic } from '@/i18n';
import { formatDate, formatTime, formatWallClock } from '@/i18n/formatters';

const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

export function displayLocation(value?: string | null): string | null {
  if (!value) return null;
  if (value === 'Prague') return t('labels.location.Prague');
  if (value === 'GL-HQ' || value === DEMO_FACILITY_ID) return t('labels.location.GL-HQ');
  if (value === 'GL-TEAMS') return t('labels.location.GL-TEAMS');
  return isGuid(value) ? t('labels.location.selected') : value;
}

export function displaySlot(value?: string | null): string | null {
  if (!value) return null;
  if (isGuid(value)) return t('labels.slot.assigned');
  const match = value.match(/^Prague-(.+)$/);
  return match ? t('labels.slot.spacePrefix', { id: match[1] }) : value;
}

// UX008 (#781) — employee-safe module labels for the module-aware reservations
// surface, mirroring the web displayLabels helpers. Booking items default to
// Parking; unknown future modules fall back to the raw resource type.
export function isSeatsItem(item: { resourceType?: string | null }): boolean {
  return item.resourceType === 'Seats';
}

export function displayModule(resourceType?: string | null): string {
  if (!resourceType || resourceType === 'Parking') return t('labels.module.Parking');
  if (resourceType === 'Seats') return t('labels.module.Seats');
  return resourceType;
}

// The employee-facing name of the allocated resource: parking uses "Spot",
// seats use "Seat". Keeps card/detail copy business-readable per module.
export function displayResourceNoun(resourceType?: string | null): string {
  return resourceType === 'Seats' ? t('labels.resourceNoun.seat') : t('labels.resourceNoun.spot');
}

// The plural form of the resource noun ("spots"/"seats"), used in copy like
// "Available {noun}" where English pluralization ("+s") doesn't carry over
// to Czech grammar.
export function displayResourceNounPlural(resourceType?: string | null): string {
  return resourceType === 'Seats' ? t('labels.resourceNounPlural.seat') : t('labels.resourceNounPlural.spot');
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
  if (!cutOffAt) return t('common.notAvailable');
  try {
    return formatTime(new Date(cutOffAt), { hour: '2-digit', minute: '2-digit', timeZone, timeZoneName: 'short' });
  } catch {
    return cutOffAt;
  }
}

// LOC002 (#799): schedule/cannot-request copy localized from the stable
// draw-status machine codes; safeMessage / cannotRequestReason free text is
// only the fallback for missing or unknown codes (older servers).
type ScheduleLike = {
  status?: string | null;
  requestWindowStatus?: string | null;
  scheduleStatus?: string | null;
  cutOffAt?: string | null;
  timeZone?: string | null;
  safeMessage?: string | null;
  cannotRequestReason?: string | null;
  scheduleMessageCode?: string | null;
  cannotRequestCode?: string | null;
};

export function displayScheduleMessage(s: ScheduleLike): string | null {
  switch (s.scheduleMessageCode) {
    case 'schedule.allocationComplete': return t('labels.schedule.allocationComplete');
    case 'schedule.notConfigured': return t('labels.schedule.notConfigured');
    case 'schedule.windowClosed': return t('labels.schedule.windowClosed');
    case 'schedule.openUntil':
      if (s.cutOffAt && s.timeZone) return t('labels.schedule.openUntil', { time: formatCutOffAt(s.cutOffAt, s.timeZone) });
      break;
  }
  if (s.status === 'Completed') return t('labels.schedule.allocationComplete');
  if (s.scheduleStatus === 'notConfigured') return t('labels.schedule.notConfigured');
  if (s.requestWindowStatus === 'closed') return t('labels.schedule.windowClosed');
  if (s.requestWindowStatus === 'open' && s.cutOffAt && s.timeZone) {
    return t('labels.schedule.openUntil', { time: formatCutOffAt(s.cutOffAt, s.timeZone) });
  }
  return s.safeMessage || null;
}

export function displayCannotRequestReason(s: ScheduleLike): string | null {
  switch (s.cannotRequestCode) {
    case 'request.datePassed': return t('labels.schedule.datePassed');
    case 'request.allocationComplete': return t('labels.schedule.allocationCompleteShort');
    case 'request.drawInProgress': return t('labels.schedule.drawInProgress');
    case 'request.windowClosed': return displayScheduleMessage(s);
  }
  if (!s.cannotRequestReason && !s.cannotRequestCode) return null;
  if (s.status === 'Completed') return t('labels.schedule.allocationCompleteShort');
  if (s.status === 'InProgress') return t('labels.schedule.drawInProgress');
  if (s.requestWindowStatus === 'closed') return displayScheduleMessage(s);
  return s.cannotRequestReason ?? null;
}

export function humanizeRejectionReason(rejectionCode: string | null, reason: string | null): string {
  if (reason) return reason;
  if (rejectionCode) {
    return tDynamic('labels.rejection', rejectionCode, t('labels.rejection.fallback'));
  }
  return t('labels.rejection.fallback');
}

export function formatBookingRef(requestId: string, requestedDate?: string): string {
  const datePart = requestedDate
    ? requestedDate.replace(/-/g, '')
    : new Date().toISOString().slice(0, 10).replace(/-/g, '');
  const shortCode = requestId.replace(/-/g, '').slice(-4).toUpperCase();
  return `BK-${datePart}-${shortCode}`;
}

export function formatRoles(roles: string[]): string {
  const seen = new Set<string>();
  const labels: string[] = [];
  for (const r of roles) {
    const label = tDynamic('labels.role', r, r);
    if (!seen.has(label)) {
      seen.add(label);
      labels.push(label);
    }
  }
  return labels.join(', ') || t('labels.role.Employee');
}

export function statusBadgeLabel(status: string): string {
  return tDynamic('labels.statusBadge', status, status);
}

export function displayDemandLevel(level: string | null | undefined): { label: string; explanation: string } | null {
  if (!level || level === 'Unknown') return null;
  const shortLabel = tDynamic('labels.demand', level, level);
  return {
    label: t('labels.demandLabel', { level: shortLabel }),
    explanation: tDynamic('labels.demandExplanation', level, t('labels.demandExplanation.Unknown')),
  };
}

// Short demand label without the "Demand: " prefix (booking/[requestId].tsx
// row values, e.g. "Low" / "Nízká").
export function demandShortLabel(level: string): string {
  return tDynamic('labels.demand', level, level);
}

// Vehicle type display label (VEHICLE_TYPES in app/(tabs)/new.tsx) — the
// wire value (e.g. "Sedan") stays in the submitted payload; only the shown
// label is localized.
export function displayVehicleType(vehicleType: string): string {
  return tDynamic('labels.vehicleType', vehicleType, vehicleType);
}

// Time preset display name (TIME_PRESETS in app/(tabs)/new.tsx), keyed by
// the preset's stable `key`.
export function displayTimePreset(presetKey: string, fallback: string): string {
  return tDynamic('labels.timePreset', presetKey, fallback);
}
