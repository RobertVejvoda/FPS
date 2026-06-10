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

export function nextWorkdayOptions(baseDate: Date, count: number): DateOption[] {
  const base = new Date(baseDate);
  base.setHours(0, 0, 0, 0);

  const options: DateOption[] = [];
  let candidate = new Date(base);

  while (options.length < count) {
    if (isWorkday(candidate)) {
      options.push({
        date: toLocalDateString(candidate),
        label: labelRelativeWorkday(base, candidate),
      });
    }
    candidate = addCalendarDays(candidate, 1);
  }

  return options;
}

export function labelRelativeWorkday(baseDate: Date, date: Date): string {
  const base = new Date(baseDate);
  base.setHours(0, 0, 0, 0);
  const target = new Date(date);
  target.setHours(0, 0, 0, 0);
  const offsetDays = Math.round((target.getTime() - base.getTime()) / 86_400_000);

  if (offsetDays === 0) return 'Today';
  if (offsetDays === 1) return 'Tomorrow';
  return target.toLocaleDateString(undefined, { weekday: 'long' });
}
