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
