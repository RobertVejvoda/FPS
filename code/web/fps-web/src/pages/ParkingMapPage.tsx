import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { canAccessHrOperations } from '../auth/roles';
import { fetchSlotMap, type SlotMapDto } from '../api/configuration';
import { fetchHrBookings, type HrBookingListItem } from '../api/bookings';
import { fetchHrDisplayNames } from '../api/profile';
import { displayLocation } from '../displayLabels';
import { useTenantDateContext } from '../hooks/useTenantDateBase';
import { DateFilter } from '../components/DateFilter';
import { toLocalDateString } from '../dateOptions';
import { compareSlotLabels, parseSlotLabel, type SlotLabel } from '../slotLabel';
import { SlotDetailDrawer } from './SlotDetailDrawer';

const LOCATION_ID = 'Prague';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ok'; slots: SlotMapDto[] }
  | { kind: 'error'; message: string };

// Keep the requestorRef on each allocation so the slot detail drawer can
// fall back to a short ref when the display-name lookup misses or fails —
// matching the recent-allocation rows (Codex review #1 on PR #473).
type AllocationMap = Record<string, { displayName: string | null; status: string; requestorRef: string }>;

export function ParkingMapPage() {
  const { apiBaseUrl, bearerToken, roles, clear } = useAuth();
  const navigate = useNavigate();
  const isHr = canAccessHrOperations(roles);

  const { dateBase, simulationActive } = useTenantDateContext();

  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [allocations, setAllocations] = useState<AllocationMap>({});
  // Initial date comes from `dateBase` so simulation mode opens the map on
  // the virtual day matching the chip presets — not on real wall time (Codex
  // review on PR #485). userPickedDate tracks whether the user has explicitly
  // chosen a date so we don't overwrite their selection when dateBase moves.
  const [selectedDate, setSelectedDateRaw] = useState<string>(() => toLocalDateString(dateBase));
  const userPickedDate = useRef(false);
  function setSelectedDate(next: string) {
    userPickedDate.current = true;
    setSelectedDateRaw(next);
  }
  useEffect(() => {
    if (userPickedDate.current) return;
    setSelectedDateRaw(toLocalDateString(dateBase));
  }, [dateBase]);
  const [detailSlot, setDetailSlot] = useState<SlotMapDto | null>(null);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    void fetchSlotMap({ apiBaseUrl, bearerToken }, LOCATION_ID).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', slots: result.data });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load parking map.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  // HR-only allocation overlay for the selected date.
  useEffect(() => {
    if (!isHr) return;
    setAllocations({});
    void fetchHrBookings({ apiBaseUrl, bearerToken }, {
      locationId: LOCATION_ID, from: selectedDate, to: selectedDate, status: 'Allocated',
    }).then(async result => {
      if (result.kind !== 'ok') return;
      const allocated = result.items.filter((i: HrBookingListItem) => i.allocatedSlotId);
      const map: AllocationMap = {};
      for (const item of allocated) {
        if (item.allocatedSlotId) {
          map[item.allocatedSlotId] = { displayName: null, status: item.status, requestorRef: item.requestorRef };
        }
      }
      const refs = [...new Set(allocated.map(i => i.requestorRef).filter(Boolean))];
      if (refs.length > 0) {
        const names = await fetchHrDisplayNames({ apiBaseUrl, bearerToken }, refs);
        if (names.kind === 'ok') {
          for (const item of allocated) {
            if (item.allocatedSlotId) {
              map[item.allocatedSlotId].displayName = names.data.names[item.requestorRef] ?? null;
            }
          }
        }
      }
      setAllocations(map);
    });
  }, [apiBaseUrl, bearerToken, selectedDate, isHr]);

  const grouped = useMemo(() => {
    if (state.kind !== 'ok') return new Map<string, Array<{ slot: SlotMapDto; label: SlotLabel }>>();
    const map = new Map<string, Array<{ slot: SlotMapDto; label: SlotLabel }>>();
    for (const slot of state.slots) {
      const label = parseSlotLabel(slot.slotId);
      const existing = map.get(label.floorLabel) ?? [];
      existing.push({ slot, label });
      map.set(label.floorLabel, existing);
    }
    for (const arr of map.values()) {
      arr.sort((a, b) => compareSlotLabels(a.label, b.label));
    }
    return map;
  }, [state]);

  const summary = useMemo(() => {
    if (state.kind !== 'ok') return null;
    const slots = state.slots;
    return {
      total: slots.length,
      active: slots.filter(s => s.isActive).length,
      inactive: slots.filter(s => !s.isActive).length,
      available: slots.filter(s => s.isActive && !s.isCompanyCarOnly && !s.isReserved).length,
      companyCar: slots.filter(s => s.isCompanyCarOnly).length,
      reserved: slots.filter(s => s.isReserved).length,
      ev: slots.filter(s => s.hasCharger).length,
      accessible: slots.filter(s => s.isAccessible).length,
      // Motorcycle capacity counts motorcycle units (not physical motorcycle slots),
      // matching the booking-side allocation unit a motorcycle actually consumes.
      motorcycle: slots
        .filter(s => s.isMotorcycleCapacity)
        .reduce((sum, s) => sum + (s.motorcycleCapacityUnits || 0), 0),
    };
  }, [state]);

  return (
    <div className="page-stack">
      <div className="page-hero">
        <div>
          <h2 style={{ margin: 0 }}>Parking Map</h2>
          <p style={{ margin: '0.25rem 0 0 0', color: '#64748b' }}>
            {displayLocation(LOCATION_ID) ?? LOCATION_ID}
          </p>
        </div>
      </div>

      <div className="panel">
        {state.kind === 'loading' && <p style={{ color: '#6b7280', fontSize: '0.875rem' }}>Loading parking map…</p>}
        {state.kind === 'error' && (
          <div>
            <p style={{ color: '#ef4444', fontSize: '0.875rem' }}>{state.message}</p>
            <button onClick={load} className="btn-primary">Retry</button>
          </div>
        )}
        {state.kind === 'ok' && summary && (
          <>
            {/* Capacity summary */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '0.5rem', marginBottom: '1.25rem' }}>
              <CapacityCard label="Total" value={summary.total} tone="primary" />
              <CapacityCard label="Available" value={summary.available} tone="ok" />
              <CapacityCard label="Company car" value={summary.companyCar} tone="info" />
              <CapacityCard label="Reserved" value={summary.reserved} tone="warn" />
              <CapacityCard label="EV charger" value={summary.ev} tone="info" />
              <CapacityCard label="Accessible" value={summary.accessible} tone="info" />
              <CapacityCard label="Motorcycle" value={summary.motorcycle} tone="info" />
              <CapacityCard label="Inactive" value={summary.inactive} tone="muted" />
            </div>

            {/* HR-only allocation date filter — shared component (issue #476) */}
            {isHr && (
              <div style={{ marginBottom: '1rem' }}>
                <DateFilter
                  mode="day"
                  label="Allocations for"
                  value={selectedDate}
                  onChange={setSelectedDate}
                  dateBase={dateBase}
                  simulationActive={simulationActive}
                  presetCount={4}
                />
              </div>
            )}

            {/* Floors */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
              {[...grouped.entries()].map(([floorLabel, items]) => (
                <FloorSection
                  key={floorLabel}
                  floorLabel={floorLabel}
                  items={items}
                  allocations={allocations}
                  isHr={isHr}
                  onSelectSlot={setDetailSlot}
                />
              ))}
            </div>

            {/* Legend */}
            <Legend isHr={isHr} />
          </>
        )}
      </div>

      {/* HR-only slot detail drawer (issue #471). Backend role guard means
          non-HR users would 403 anyway, but the click is also gated client-side
          so the affordance never appears for employees. */}
      {detailSlot && (
        <SlotDetailDrawer
          slot={detailSlot}
          locationId={LOCATION_ID}
          selectedDate={isHr ? selectedDate : null}
          selectedDayOccupant={allocations[detailSlot.slotId]}
          onClose={() => setDetailSlot(null)}
        />
      )}
    </div>
  );
}

function FloorSection({
  floorLabel, items, allocations, isHr, onSelectSlot,
}: {
  floorLabel: string;
  items: Array<{ slot: SlotMapDto; label: SlotLabel }>;
  allocations: AllocationMap;
  isHr: boolean;
  onSelectSlot: (slot: SlotMapDto) => void;
}) {
  return (
    <section>
      <h3 style={{ fontSize: '0.75rem', fontWeight: 700, color: '#64748b', textTransform: 'uppercase',
        letterSpacing: '0.04em', margin: '0 0 0.5rem 0' }}>
        {floorLabel}
        <span style={{ marginLeft: 8, color: '#94a3b8', fontWeight: 500 }}>
          {items.length} space{items.length === 1 ? '' : 's'}
        </span>
      </h3>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))', gap: '0.5rem' }}>
        {items.map(({ slot, label }) => (
          <SlotTile
            key={slot.slotId}
            slot={slot}
            label={label}
            allocation={allocations[slot.slotId]}
            isHr={isHr}
            onSelect={isHr ? () => onSelectSlot(slot) : undefined}
          />
        ))}
      </div>
    </section>
  );
}

function tileStatus(slot: SlotMapDto, allocation: AllocationMap[string] | undefined) {
  if (!slot.isActive) return { kind: 'inactive', bg: '#f3f4f6', color: '#6b7280', border: '#e5e7eb', label: 'Inactive' };
  if (allocation) return { kind: 'allocated', bg: '#eef2ff', color: '#3730a3', border: '#c7d2fe', label: 'Allocated' };
  if (slot.isReserved) return { kind: 'reserved', bg: '#fffbeb', color: '#92400e', border: '#fcd34d', label: 'Reserved' };
  if (slot.isCompanyCarOnly) return { kind: 'company', bg: '#ecfeff', color: '#155e75', border: '#a5f3fc', label: 'Company car' };
  return { kind: 'available', bg: '#f0fdf4', color: '#166534', border: '#bbf7d0', label: 'Available' };
}

function SlotTile({
  slot, label, allocation, isHr, onSelect,
}: {
  slot: SlotMapDto;
  label: SlotLabel;
  allocation: AllocationMap[string] | undefined;
  isHr: boolean;
  onSelect?: () => void;
}) {
  const status = tileStatus(slot, allocation);
  // HR tiles are interactive (button); employee tiles stay as plain divs so the
  // history endpoint is unreachable from the employee surface (no role guard
  // bypass via tile click).
  const Tag: 'button' | 'div' = onSelect ? 'button' : 'div';
  const interactiveProps = onSelect
    ? { onClick: onSelect, type: 'button' as const, 'aria-label': `Open ${label.longLabel} history` }
    : {};
  return (
    <Tag
      title={label.longLabel}
      {...interactiveProps}
      style={{
        borderRadius: 8, border: `1px solid ${status.border}`, background: status.bg,
        color: status.color, padding: '0.5rem 0.625rem', display: 'flex', flexDirection: 'column', gap: 4, minHeight: 64,
        textAlign: 'left', cursor: onSelect ? 'pointer' : 'default', font: 'inherit',
      }}>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 4 }}>
        <span style={{ fontSize: '0.95rem', fontWeight: 700 }}>{label.shortLabel}</span>
        <span style={{ fontSize: '0.65rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.04em', opacity: 0.85 }}>
          {status.label}
        </span>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
        {slot.hasCharger && <Chip label="EV" />}
        {slot.isAccessible && <Chip label="♿" />}
        {slot.isCompanyCarOnly && <Chip label="Co. car" />}
        {slot.isMotorcycleCapacity && (
          <Chip label={slot.motorcycleCapacityUnits > 1 ? `MC × ${slot.motorcycleCapacityUnits}` : 'MC'} />
        )}
        {slot.isReserved && <Chip label="Res" />}
      </div>
      {isHr && allocation?.displayName && (
        <div style={{ fontSize: '0.72rem', fontWeight: 600, color: '#1e293b', marginTop: 2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {allocation.displayName}
        </div>
      )}
    </Tag>
  );
}

function Chip({ label }: { label: string }) {
  return (
    <span style={{ fontSize: '0.65rem', fontWeight: 600, background: 'rgba(255,255,255,0.55)',
      border: '1px solid rgba(15,23,42,0.08)', borderRadius: 10, padding: '1px 6px', color: 'inherit',
      lineHeight: '14px' }}>
      {label}
    </span>
  );
}

function CapacityCard({ label, value, tone }: { label: string; value: number; tone: 'primary' | 'ok' | 'info' | 'warn' | 'muted' }) {
  const toneStyle = (() => {
    switch (tone) {
      case 'primary': return { bg: '#eff6ff', border: '#bfdbfe', color: '#1d4ed8' };
      case 'ok':      return { bg: '#f0fdf4', border: '#bbf7d0', color: '#166534' };
      case 'info':    return { bg: '#ecfeff', border: '#a5f3fc', color: '#155e75' };
      case 'warn':    return { bg: '#fffbeb', border: '#fcd34d', color: '#92400e' };
      case 'muted':   return { bg: '#f9fafb', border: '#e5e7eb', color: '#6b7280' };
    }
  })();
  return (
    <div style={{ padding: '0.5rem 0.875rem', borderRadius: 8, border: `1px solid ${toneStyle.border}`,
      background: toneStyle.bg, color: toneStyle.color }}>
      <div style={{ fontSize: '0.7rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em', opacity: 0.85 }}>
        {label}
      </div>
      <div style={{ fontSize: '1.4rem', fontWeight: 700, marginTop: 2 }}>{value}</div>
    </div>
  );
}

function Legend({ isHr }: { isHr: boolean }) {
  return (
    <div style={{ marginTop: '1.25rem', padding: '0.75rem', background: '#fafafa', borderRadius: 6,
      border: '1px solid #e5e7eb', display: 'flex', gap: '0.75rem', flexWrap: 'wrap', alignItems: 'center', fontSize: '0.75rem', color: '#475569' }}>
      <strong style={{ marginRight: 4 }}>Legend:</strong>
      <LegendSwatch label="Available" bg="#f0fdf4" border="#bbf7d0" />
      {isHr && <LegendSwatch label="Allocated" bg="#eef2ff" border="#c7d2fe" />}
      <LegendSwatch label="Company car" bg="#ecfeff" border="#a5f3fc" />
      <LegendSwatch label="Reserved" bg="#fffbeb" border="#fcd34d" />
      <LegendSwatch label="Inactive" bg="#f3f4f6" border="#e5e7eb" />
      <span style={{ marginLeft: 'auto', color: '#94a3b8' }}>
        EV = charger · ♿ = accessible · Co. car = company car · MC = motorcycle · Res = reserved
      </span>
    </div>
  );
}

function LegendSwatch({ label, bg, border }: { label: string; bg: string; border: string }) {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
      <span style={{ display: 'inline-block', width: 12, height: 12, borderRadius: 3, background: bg, border: `1px solid ${border}` }} />
      {label}
    </span>
  );
}
