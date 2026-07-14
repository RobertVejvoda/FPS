import { useEffect, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { fetchHrSlotHistory, type HrSlotHistoryItem } from '../api/bookings';
import { fetchHrDisplayNames } from '../api/profile';
import type { SlotMapDto } from '../api/configuration';
import {
  displayDate,
  displayDateTime,
  displayLocation,
  displayRequestorRef,
  humanizeHrRejection,
} from '../displayLabels';
import { parseSlotLabel } from '../slotLabel';
import { t, tDynamic } from '../i18n';

interface Props {
  slot: SlotMapDto;
  locationId: string;
  selectedDate: string | null;
  selectedDayOccupant: { displayName: string | null; status: string; requestorRef: string } | undefined;
  onClose: () => void;
}

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: HrSlotHistoryItem[]; totalCount: number }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

function nDaysAgoIso(n: number): string {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function statusBadgeStyle(status: string): React.CSSProperties {
  switch (status) {
    case 'Allocated': return { background: '#f0fdf4', color: '#166534', border: '1px solid #bbf7d0' };
    case 'Pending':   return { background: '#fffbeb', color: '#92400e', border: '1px solid #fcd34d' };
    case 'Cancelled':
    case 'NoShow':
    case 'Expired':   return { background: '#f9fafb', color: '#6b7280', border: '1px solid #e5e7eb' };
    case 'Rejected':  return { background: '#fef2f2', color: '#991b1b', border: '1px solid #fecaca' };
    default:          return { background: '#f9fafb', color: '#6b7280', border: '1px solid #e5e7eb' };
  }
}

export function SlotDetailDrawer({ slot, locationId, selectedDate, selectedDayOccupant, onClose }: Props) {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [state, setState] = useState<LoadState>({ kind: 'loading' });
  const [names, setNames] = useState<Record<string, string | null>>({});

  const label = parseSlotLabel(slot.slotId);

  useEffect(() => {
    let cancelled = false;
    setState({ kind: 'loading' });
    void fetchHrSlotHistory({ apiBaseUrl, bearerToken }, slot.slotId, {
      locationId,
      from: nDaysAgoIso(30),
      to: todayIso(),
      pageSize: 50,
    }).then(async result => {
      if (cancelled) return;
      if (result.kind === 'unauthenticated' || result.kind === 'forbidden') {
        setState({ kind: 'forbidden' });
        return;
      }
      if (result.kind !== 'ok') {
        setState({ kind: 'error', message: 'message' in result ? result.message : t('bookings.history.loadError') });
        return;
      }
      setState({ kind: 'ok', items: result.items, totalCount: result.totalCount });

      const refs = [...new Set(result.items.map(i => i.requestorRef).filter(Boolean))];
      if (refs.length > 0) {
        const nameResult = await fetchHrDisplayNames({ apiBaseUrl, bearerToken }, refs);
        if (!cancelled && nameResult.kind === 'ok') setNames(nameResult.data.names);
      }
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, slot.slotId, locationId]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => { window.removeEventListener('keydown', onKey); };
  }, [onClose]);

  return (
    <div role="dialog" aria-modal="true" aria-label={t('bookings.slotDrawer.detailAriaLabel', { slot: label.longLabel })}
         style={{ position: 'fixed', inset: 0, zIndex: 200, display: 'flex' }}>
      <div onClick={onClose} style={{ flex: 1, background: 'rgba(15, 23, 42, 0.4)' }} />
      <aside style={{
        width: '100%', maxWidth: 480, background: '#fff', boxShadow: '-4px 0 16px rgba(15, 23, 42, 0.08)',
        display: 'flex', flexDirection: 'column', overflow: 'hidden'
      }}>
        <header style={{ padding: '1rem 1.25rem', borderBottom: '1px solid #e5e7eb', display: 'flex',
          alignItems: 'flex-start', justifyContent: 'space-between', gap: '0.75rem' }}>
          <div style={{ minWidth: 0 }}>
            <div style={{ fontSize: '1rem', fontWeight: 700, color: '#0f172a' }}>{label.longLabel}</div>
            <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: 2 }}>
              {displayLocation(locationId) ?? locationId}
            </div>
          </div>
          <button onClick={onClose} aria-label={t('bookings.slotDrawer.close')}
            style={{ background: 'transparent', border: 'none', cursor: 'pointer', fontSize: '1.1rem',
              padding: '0.25rem 0.5rem', borderRadius: 4, color: '#64748b', flexShrink: 0 }}>
            ✕
          </button>
        </header>

        <div style={{ flex: 1, overflow: 'auto', padding: '1rem 1.25rem', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          <CapabilitiesSection slot={slot} />

          {selectedDate && (
            <SelectedDaySection
              date={selectedDate}
              occupant={selectedDayOccupant}
              slotInactive={!slot.isActive}
              slotReserved={slot.isReserved}
            />
          )}

          <HistorySection state={state} names={names} />
        </div>
      </aside>
    </div>
  );
}

function CapabilitiesSection({ slot }: { slot: SlotMapDto }) {
  return (
    <section>
      <SectionHeading>{t('bookings.slotDrawer.capabilities')}</SectionHeading>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
        {!slot.isActive && <Chip tone="muted" label={t('bookings.slotDrawer.inactive')} />}
        {slot.hasCharger && <Chip tone="info" label={t('bookings.slotDrawer.evCharger')} />}
        {slot.isAccessible && <Chip tone="info" label={t('bookings.slotDrawer.accessible')} />}
        {slot.isCompanyCarOnly && <Chip tone="info" label={t('bookings.field.companyCar')} />}
        {slot.isMotorcycleCapacity && (
          <Chip tone="info" label={slot.motorcycleCapacityUnits > 1 ? t('bookings.slotDrawer.motorcycleUnits', { count: slot.motorcycleCapacityUnits }) : t('bookings.slotDrawer.motorcycle')} />
        )}
        {slot.isReserved && <Chip tone="warn" label={t('bookings.slotDrawer.reserved')} />}
        {slot.isActive && !slot.isReserved && !slot.isCompanyCarOnly && !slot.isMotorcycleCapacity && (
          <Chip tone="ok" label={t('bookings.slotDrawer.general')} />
        )}
      </div>
    </section>
  );
}

function SelectedDaySection({
  date, occupant, slotInactive, slotReserved,
}: {
  date: string;
  occupant: { displayName: string | null; status: string; requestorRef: string } | undefined;
  slotInactive: boolean;
  slotReserved: boolean;
}) {
  return (
    <section>
      <SectionHeading>{t('bookings.slotDrawer.onDate', { date: displayDate(date) })}</SectionHeading>
      {occupant ? (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: '0.85rem', fontWeight: 600, color: '#0f172a' }}>
            {/* Fall back to the short requestor ref when display-name lookup
                misses or fails — matches the recent-allocation row behaviour
                and avoids the bare "Allocated" label on seeded/stale refs. */}
            {occupant.displayName ?? displayRequestorRef(occupant.requestorRef)}
          </span>
          <span style={{ fontSize: '0.7rem', fontWeight: 600, padding: '0.15rem 0.6rem', borderRadius: 12, ...statusBadgeStyle(occupant.status) }}>
            {tDynamic('bookings.status', occupant.status, occupant.status)}
          </span>
        </div>
      ) : (
        <p style={{ margin: 0, fontSize: '0.85rem', color: '#6b7280' }}>
          {slotInactive ? t('bookings.slotDrawer.slotInactive') :
            slotReserved ? t('bookings.slotDrawer.reservedCapacity') :
            t('bookings.slotDrawer.noAllocation')}
        </p>
      )}
    </section>
  );
}

function HistorySection({
  state, names,
}: {
  state: LoadState;
  names: Record<string, string | null>;
}) {
  return (
    <section>
      <SectionHeading>{t('bookings.slotDrawer.recentAllocations')}</SectionHeading>
      {state.kind === 'loading' && (
        <p style={{ color: '#6b7280', fontSize: '0.85rem', margin: 0 }}>{t('common.loading')}</p>
      )}
      {state.kind === 'forbidden' && (
        <p style={{ color: '#991b1b', fontSize: '0.85rem', margin: 0 }}>
          {t('bookings.slotDrawer.forbidden')}
        </p>
      )}
      {state.kind === 'error' && (
        <div style={{ background: '#fef2f2', border: '1px solid #fecaca', color: '#991b1b',
          borderRadius: 6, padding: '0.5rem 0.625rem', fontSize: '0.85rem' }}>
          {state.message}
        </div>
      )}
      {state.kind === 'ok' && state.items.length === 0 && (
        <p style={{ color: '#6b7280', fontSize: '0.85rem', margin: 0 }}>
          {t('bookings.slotDrawer.noneInWindow')}
        </p>
      )}
      {state.kind === 'ok' && state.items.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
          {state.items.map(item => (
            <HistoryRow key={item.requestId} item={item} displayName={names[item.requestorRef] ?? null} />
          ))}
          {state.totalCount > state.items.length && (
            <p style={{ fontSize: '0.7rem', color: '#94a3b8', margin: '0.25rem 0 0 0' }}>
              {t('bookings.slotDrawer.showingOf', { shown: state.items.length, total: state.totalCount })}
            </p>
          )}
        </div>
      )}
    </section>
  );
}

function HistoryRow({ item, displayName }: { item: HrSlotHistoryItem; displayName: string | null }) {
  const timeWindow = item.timeSlotStart && item.timeSlotEnd
    ? `${item.timeSlotStart.slice(0, 5)}–${item.timeSlotEnd.slice(0, 5)}`
    : null;
  const reasonText = (item.reasonCode || item.reason)
    ? humanizeHrRejection(item.reasonCode ?? null, item.reason ?? null)
    : null;
  const primary = displayName ?? displayRequestorRef(item.requestorRef);

  return (
    <div style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: 6, padding: '0.5rem 0.625rem' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
        <span style={{ fontSize: '0.85rem', fontWeight: 600, color: '#0f172a' }}>{primary}</span>
        <span style={{ fontSize: '0.7rem', fontWeight: 600, padding: '0.15rem 0.6rem', borderRadius: 12, ...statusBadgeStyle(item.status) }}>
          {tDynamic('bookings.status', item.status, item.status)}
        </span>
      </div>
      <div style={{ marginTop: 3, display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.75rem', color: '#475569', flexWrap: 'wrap' }}>
        <span style={{ fontWeight: 600, color: '#1e293b' }}>{displayDate(item.requestedDate)}</span>
        {timeWindow && <><span style={{ color: '#cbd5e1' }}>·</span><span>{timeWindow}</span></>}
        <span style={{ color: '#cbd5e1' }}>·</span>
        <span style={{ color: '#94a3b8' }}>{t('bookings.slotDrawer.updated', { date: displayDateTime(item.lastStatusChangedAt) })}</span>
      </div>
      {reasonText && (
        <div style={{ marginTop: 4, fontSize: '0.75rem', color: '#92400e', background: '#fffbeb',
          border: '1px solid #fcd34d', borderRadius: 4, padding: '0.25rem 0.5rem', display: 'inline-block' }}>
          {reasonText}
        </div>
      )}
    </div>
  );
}

function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h3 style={{ fontSize: '0.7rem', fontWeight: 700, color: '#64748b', textTransform: 'uppercase',
      letterSpacing: '0.04em', margin: '0 0 0.5rem 0' }}>
      {children}
    </h3>
  );
}

function Chip({ label, tone }: { label: string; tone: 'ok' | 'info' | 'warn' | 'muted' }) {
  const toneStyle = (() => {
    switch (tone) {
      case 'ok':    return { bg: '#f0fdf4', border: '#bbf7d0', color: '#166534' };
      case 'info':  return { bg: '#ecfeff', border: '#a5f3fc', color: '#155e75' };
      case 'warn':  return { bg: '#fffbeb', border: '#fcd34d', color: '#92400e' };
      case 'muted': return { bg: '#f3f4f6', border: '#e5e7eb', color: '#6b7280' };
    }
  })();
  return (
    <span style={{ fontSize: '0.7rem', fontWeight: 600, padding: '2px 8px', borderRadius: 12,
      background: toneStyle.bg, border: `1px solid ${toneStyle.border}`, color: toneStyle.color }}>
      {label}
    </span>
  );
}
