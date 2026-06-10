export type DateOption = {
  date: string;
  label: string;
};

export function toLocalDateString(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

export function fromLocalDateString(date: string): Date {
  return new Date(`${date}T00:00:00`);
}

export function addCalendarDays(date: Date, days: number): Date {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

export function isWorkday(date: Date): boolean {
  const day = date.getDay();
  return day >= 1 && day <= 5;
}

export function nextWorkdayOptions(baseDate: Date, count: number, config?: { relativeLabels?: boolean }): DateOption[] {
  const base = new Date(baseDate);
  base.setHours(0, 0, 0, 0);
  const relativeLabels = config?.relativeLabels ?? true;

  const result: DateOption[] = [];
  let candidate = new Date(base);

  while (result.length < count) {
    if (isWorkday(candidate)) {
      result.push({
        date: toLocalDateString(candidate),
        label: relativeLabels ? labelRelativeWorkday(base, candidate) : labelWeekdayDate(candidate),
      });
    }
    candidate = addCalendarDays(candidate, 1);
  }

  return result;
}

export function labelRelativeWorkday(baseDate: Date, date: Date): string {
  const base = new Date(baseDate);
  base.setHours(0, 0, 0, 0);
  const target = new Date(date);
  target.setHours(0, 0, 0, 0);
  const offsetDays = Math.round((target.getTime() - base.getTime()) / 86_400_000);
  const dateLabel = labelShortDate(target);

  if (offsetDays === 0) return `Today · ${dateLabel}`;
  if (offsetDays === 1) return `Tomorrow · ${dateLabel}`;
  return `${target.toLocaleDateString(undefined, { weekday: 'long' })} · ${dateLabel}`;
}

export function labelWeekdayDate(date: Date): string {
  return date.toLocaleDateString(undefined, { weekday: 'long', month: 'short', day: 'numeric' });
}

function labelShortDate(date: Date): string {
  return date.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
}
