import { useEffect, useMemo, useState } from 'react';
import { nextWorkdayOptions, toLocalDateString } from '../dateOptions';
import { t, type MessageKey } from '../i18n';

// Shared date filter (issue #476). One component, two modes:
//
//   - `day` mode: pick a single date. Default presets are Today + the next
//     N workdays in the tenant's business time. Custom date picker collapses
//     behind a "Custom date" toggle.
//   - `range` mode: pick a from/to range. Presets cover the common ops
//     windows (Today, Yesterday, This week, Last week, This month, Last
//     month, All time). Custom from/to inputs collapse behind a "Custom
//     range" toggle.
//
// Both modes reuse the tenant's `dateBase` so simulation-mode pages compute
// presets against virtual time. The component never reads time directly.

type DayProps = {
  mode: 'day';
  value: string;                // YYYY-MM-DD, never empty — the consumer always has a selected day.
  onChange: (next: string) => void;
  dateBase: Date;
  simulationActive: boolean;
  presetCount?: number;         // how many workdays after today to surface; default 4 (matches HrOperations chip pattern).
  label?: string;               // optional leading label, e.g. "Allocations for".
};

// Range filter value. When a preset (Today, This week, etc.) is active,
// `presetKey` is set and the component re-emits with fresh bounds whenever
// `dateBase` changes — so simulation/real clock ticks don't freeze the
// semantic window into a stale absolute date (Codex review on PR #485
// flagged this regression vs. the previous Auditor implementation that
// kept the preset key). When the user types a custom range, `presetKey`
// is omitted.
export type RangePresetKey = 'All' | 'Today' | 'Yesterday' | 'ThisWeek' | 'LastWeek' | 'ThisMonth' | 'LastMonth';
export type RangeFilterValue = { after?: string; before?: string; presetKey?: RangePresetKey };

type RangeProps = {
  mode: 'range';
  value: RangeFilterValue;
  onChange: (next: RangeFilterValue) => void;
  dateBase: Date;
  label?: string;
};

type Props = DayProps | RangeProps;

export function DateFilter(props: Props) {
  if (props.mode === 'day') return <DayFilter {...props} />;
  return <RangeFilter {...props} />;
}

// ── Day mode ─────────────────────────────────────────────────────────────

function DayFilter({ value, onChange, dateBase, simulationActive, presetCount = 4, label }: DayProps) {
  const presets = useMemo(
    () => nextWorkdayOptions(dateBase, presetCount, { relativeLabels: !simulationActive }),
    [dateBase, presetCount, simulationActive],
  );

  const isPreset = presets.some(p => p.date === value);
  const [customOpen, setCustomOpen] = useState(!isPreset);

  // Keep "Custom" expanded automatically when the consumer's value is
  // outside the preset list — e.g. after switching dates from the date
  // picker, the page reloads with that date already selected.
  useEffect(() => { if (!isPreset) setCustomOpen(true); }, [isPreset]);

  return (
    <div style={containerStyle}>
      {label && <span style={labelStyle}>{label}</span>}
      <div style={chipRowStyle}>
        {presets.map(p => (
          <ChipButton
            key={p.date}
            active={value === p.date}
            onClick={() => onChange(p.date)}
            label={p.label}
          />
        ))}
        <ChipButton
          active={customOpen && !isPreset}
          onClick={() => setCustomOpen(o => !o)}
          label={customOpen ? t('dateFilter.hideCustom') : t('dateFilter.customDate')}
          subdued
        />
      </div>
      {customOpen && (
        <div style={customRowStyle}>
          <input
            type="date"
            aria-label={t('dateFilter.customDate')}
            value={value}
            onChange={e => onChange(e.target.value || toLocalDateString(dateBase))}
            style={inputStyle}
          />
        </div>
      )}
    </div>
  );
}

// ── Range mode ───────────────────────────────────────────────────────────

const RANGE_PRESET_LABEL_KEYS: Record<RangePresetKey, MessageKey> = {
  All: 'dateFilter.preset.allTime',
  Today: 'labels.date.today',
  Yesterday: 'dateFilter.preset.yesterday',
  ThisWeek: 'dateFilter.preset.thisWeek',
  LastWeek: 'dateFilter.preset.lastWeek',
  ThisMonth: 'dateFilter.preset.thisMonth',
  LastMonth: 'dateFilter.preset.lastMonth',
};

const RANGE_PRESETS: Array<{ key: RangePresetKey }> = [
  { key: 'All' },
  { key: 'Today' },
  { key: 'Yesterday' },
  { key: 'ThisWeek' },
  { key: 'LastWeek' },
  { key: 'ThisMonth' },
  { key: 'LastMonth' },
];

/**
 * Resolve a preset key into ISO-timestamp after/before bounds in the
 * tenant's business time, tagged with the preset key so callers can keep
 * the semantic identity (Codex review on PR #485). Exposed for callers
 * that need to seed initial state from a preset before mounting the
 * component.
 */
export function rangePresetToBounds(key: RangePresetKey, dateBase: Date): RangeFilterValue {
  if (key === 'All') return { presetKey: 'All' };
  const today = new Date(dateBase.getTime());
  today.setHours(0, 0, 0, 0);

  const startOf = (d: Date): string => { const c = new Date(d.getTime()); c.setHours(0, 0, 0, 0); return c.toISOString(); };
  const endOf   = (d: Date): string => { const c = new Date(d.getTime()); c.setHours(23, 59, 59, 999); return c.toISOString(); };
  const copy    = (d: Date): Date   => new Date(d.getTime());

  switch (key) {
    case 'Today':
      return { presetKey: key, after: startOf(today), before: endOf(today) };
    case 'Yesterday': {
      const y = copy(today); y.setDate(y.getDate() - 1);
      return { presetKey: key, after: startOf(y), before: endOf(y) };
    }
    case 'ThisWeek': {
      const mon = copy(today); mon.setDate(mon.getDate() - ((mon.getDay() + 6) % 7));
      return { presetKey: key, after: startOf(mon), before: endOf(today) };
    }
    case 'LastWeek': {
      const mon = copy(today); mon.setDate(mon.getDate() - ((mon.getDay() + 6) % 7) - 7);
      const sun = copy(mon); sun.setDate(sun.getDate() + 6);
      return { presetKey: key, after: startOf(mon), before: endOf(sun) };
    }
    case 'ThisMonth': {
      const first = new Date(today.getFullYear(), today.getMonth(), 1);
      return { presetKey: key, after: startOf(first), before: endOf(today) };
    }
    case 'LastMonth': {
      const first = new Date(today.getFullYear(), today.getMonth() - 1, 1);
      const last = new Date(today.getFullYear(), today.getMonth(), 0);
      return { presetKey: key, after: startOf(first), before: endOf(last) };
    }
  }
}

function RangeFilter({ value, onChange, dateBase, label }: RangeProps) {
  const activePresetKey = value.presetKey;
  const [customOpen, setCustomOpen] = useState(!activePresetKey);

  useEffect(() => { if (!activePresetKey) setCustomOpen(true); }, [activePresetKey]);

  // When a preset is active, recompute its bounds whenever dateBase changes
  // (simulation tick, real day rollover, …) so "Today" stays today instead
  // of freezing to the first absolute day the user clicked.
  useEffect(() => {
    if (!activePresetKey) return;
    const fresh = rangePresetToBounds(activePresetKey, dateBase);
    if (fresh.after !== value.after || fresh.before !== value.before) {
      onChange(fresh);
    }
    // onChange is deliberately omitted — most consumers pass a fresh
    // arrow on every render and would otherwise loop forever.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activePresetKey, dateBase]);

  // Custom inputs use YYYY-MM-DD strings; convert to ISO on emit so the
  // consumer always sees the unified ISO-timestamp shape, and back to a
  // LOCAL YYYY-MM-DD when reading — `.slice(0, 10)` would return the UTC
  // date, which shifts the picker one day earlier east of UTC (Prague
  // local midnight is 22:00 UTC the day before). Codex review on PR #485.
  const customFrom = value.after ? toLocalDateString(new Date(value.after)) : '';
  const customTo   = value.before ? toLocalDateString(new Date(value.before)) : '';

  function emitCustom(fromStr: string, toStr: string) {
    const after = fromStr ? new Date(`${fromStr}T00:00:00`).toISOString() : undefined;
    const before = toStr ? new Date(`${toStr}T23:59:59.999`).toISOString() : undefined;
    onChange({ after, before });   // no presetKey → custom mode
  }

  return (
    <div style={containerStyle}>
      {label && <span style={labelStyle}>{label}</span>}
      <div style={chipRowStyle}>
        {RANGE_PRESETS.map(p => (
          <ChipButton
            key={p.key}
            active={activePresetKey === p.key}
            onClick={() => onChange(rangePresetToBounds(p.key, dateBase))}
            label={t(RANGE_PRESET_LABEL_KEYS[p.key])}
          />
        ))}
        <ChipButton
          active={customOpen && !activePresetKey}
          onClick={() => setCustomOpen(o => !o)}
          label={customOpen ? t('dateFilter.hideCustom') : t('dateFilter.customRange')}
          subdued
        />
      </div>
      {customOpen && (
        <div style={customRowStyle}>
          <label style={customLabelStyle}>
            {t('dateFilter.from')}
            <input
              type="date"
              aria-label={t('dateFilter.customFromAria')}
              value={customFrom}
              onChange={e => emitCustom(e.target.value, customTo)}
              style={inputStyle}
            />
          </label>
          <label style={customLabelStyle}>
            {t('dateFilter.to')}
            <input
              type="date"
              aria-label={t('dateFilter.customToAria')}
              value={customTo}
              onChange={e => emitCustom(customFrom, e.target.value)}
              style={inputStyle}
            />
          </label>
        </div>
      )}
    </div>
  );
}

// ── Shared bits ──────────────────────────────────────────────────────────

function ChipButton({ active, onClick, label, subdued }: { active: boolean; onClick: () => void; label: string; subdued?: boolean }) {
  return (
    <button
      type="button"
      onClick={onClick}
      style={{
        padding: '0.375rem 0.875rem', borderRadius: 20, border: 'none', cursor: 'pointer',
        fontSize: '0.875rem',
        background: active ? '#2563eb' : (subdued ? '#fff' : '#f3f4f6'),
        color: active ? '#fff' : (subdued ? '#64748b' : '#374151'),
        fontWeight: active ? 600 : 400,
        boxShadow: subdued && !active ? 'inset 0 0 0 1px #e5e7eb' : undefined,
      }}
    >
      {label}
    </button>
  );
}

const containerStyle: React.CSSProperties = {
  display: 'flex', flexDirection: 'column', gap: '0.5rem',
};

const labelStyle: React.CSSProperties = {
  fontSize: '0.75rem', color: '#64748b', textTransform: 'uppercase', letterSpacing: '0.04em', fontWeight: 600,
};

const chipRowStyle: React.CSSProperties = {
  display: 'flex', gap: '0.5rem', flexWrap: 'wrap',
};

const customRowStyle: React.CSSProperties = {
  display: 'flex', gap: '0.625rem', alignItems: 'flex-end', flexWrap: 'wrap',
};

const customLabelStyle: React.CSSProperties = {
  display: 'flex', flexDirection: 'column', fontSize: '0.7rem', color: '#475569', gap: 2,
};

const inputStyle: React.CSSProperties = {
  padding: '0.25rem 0.5rem', fontSize: '0.85rem', border: '1px solid #d1d5db', borderRadius: 4,
};
