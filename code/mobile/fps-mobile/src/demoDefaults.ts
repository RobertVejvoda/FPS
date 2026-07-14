export const DEMO_LOCATION_ID = 'Prague';
export const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';

// UX009 (#782) — aligned with the established Parking whole-day default used by the
// web app, the demo seed, and the draw schedule (08:00–18:00). Mobile previously
// used 06:00–20:00 here, which diverged from every other Parking surface.
// Keep in sync with initialForm() in (tabs)/new.tsx.
export const DEFAULT_TIME_SLOT_START = '08:00:00';
export const DEFAULT_TIME_SLOT_END = '18:00:00';

// PLAT-seats (#710) — the showcase seats location; overridden by the employee's own
// seat-booking history when available.
export const DEFAULT_SEATS_LOCATION = 'GL-TEAMS';
