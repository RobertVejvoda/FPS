// Slot-label helpers for the Parking Map.
//
// Demo convention: a 3-digit slot id like "312" reads as "floor -3, space 12".
// We treat the leading digit as a (negated) basement floor and the remaining
// digits as the space number on that floor. Unknown formats fall back to the
// raw id so the UI never shows a bogus label.

export interface SlotLabel {
  /** Inferred floor when known (e.g. -3 for "312"); null when unknown. */
  floor: number | null;
  /** Inferred space number on the floor when known; null when unknown. */
  space: number | null;
  /** Inferred floor label for grouping, e.g. "Floor -3" or "Floor unknown". */
  floorLabel: string;
  /** Compact slot label, e.g. "-3·12" when parsed; otherwise the raw id. */
  shortLabel: string;
  /** Long-form label suitable for tooltips: "Floor -3 · Space 12" or raw id. */
  longLabel: string;
}

const UNKNOWN_FLOOR_LABEL = 'Other';

export function parseSlotLabel(slotId: string | null | undefined): SlotLabel {
  const id = (slotId ?? '').trim();
  if (!id) {
    return {
      floor: null,
      space: null,
      floorLabel: UNKNOWN_FLOOR_LABEL,
      shortLabel: '—',
      longLabel: '—',
    };
  }

  // Demo convention: leading digit = basement floor, remaining = space.
  // Require at least one floor digit AND at least one space digit, so a
  // single-digit id ("7") does not get parsed as "floor -7, space 0".
  const match = /^(\d)(\d{1,3})$/.exec(id);
  if (match) {
    const floorDigit = Number(match[1]);
    const space = Number(match[2]);
    if (Number.isFinite(floorDigit) && Number.isFinite(space)) {
      const floor = -floorDigit;
      const spaceLabel = String(space).padStart(2, '0');
      return {
        floor,
        space,
        floorLabel: `Floor ${floor}`,
        shortLabel: `${floor}·${spaceLabel}`,
        longLabel: `Floor ${floor} · Space ${spaceLabel}`,
      };
    }
  }

  return {
    floor: null,
    space: null,
    floorLabel: UNKNOWN_FLOOR_LABEL,
    shortLabel: id,
    longLabel: id,
  };
}

// Stable ordering: known floors first ascending toward street level
// (e.g. -3 before -2 before -1), then the unknown bucket last.
export function compareFloors(a: number | null, b: number | null): number {
  if (a === null && b === null) return 0;
  if (a === null) return 1;
  if (b === null) return -1;
  return a - b;
}

export function compareSlotLabels(a: SlotLabel, b: SlotLabel): number {
  const f = compareFloors(a.floor, b.floor);
  if (f !== 0) return f;
  if (a.space === null && b.space === null) return a.shortLabel.localeCompare(b.shortLabel);
  if (a.space === null) return 1;
  if (b.space === null) return -1;
  return a.space - b.space;
}
