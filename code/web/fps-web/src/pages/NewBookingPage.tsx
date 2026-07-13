import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, fetchDrawStatus, submitBooking, type DrawStatusResult, type SubmitResult } from '../api/bookings';
import { fetchProfileSnapshot, type ProfileSnapshot } from '../api/profile';
import { isWorkday, nextWorkdayOptions, toLocalDateString } from '../dateOptions';
import { useTenantDateContext } from '../hooks/useTenantDateBase';
import { useTenantModules } from '../tenant/TenantModulesContext';
import { ModuleBadge } from '../components/ModuleBadge';

const VEHICLE_TYPES = ['Compact', 'Sedan', 'SUV', 'Van', 'Truck', 'Motorcycle'] as const;
const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';
const DEMO_LOCATION_ID = 'Prague';
// PLAT-seats (#710) — the showcase seats location; overridden by the employee's own
// seat-booking history when available.
const DEFAULT_SEATS_LOCATION = 'GL-TEAMS';

// UX009 (#782) — the three preset time ranges, always shown as actual hours.
// These reuse the established Parking whole-day default (08:00–18:00) that the
// draw-status checks, seed, and day cards already use; morning/afternoon split it.
const TIME_PRESETS = [
  { key: 'day', name: 'Whole day', start: '08:00', end: '18:00' },
  { key: 'morning', name: 'Morning', start: '08:00', end: '12:00' },
  { key: 'afternoon', name: 'Afternoon', start: '12:00', end: '18:00' },
] as const;

type Form = {
  facilityId: string;
  locationId: string;
  selectedVehicleId: string;
  licensePlate: string;
  vehicleType: string;
  isElectric: boolean;
  requiresAccessibleSpot: boolean;
  isCompanyCar: boolean;
  plannedArrival: string;
  plannedDeparture: string;
};

// One entry per module submit — partial success is the default model (UX009 #782):
// a valid Seat request may be created even if Parking fails, and the reverse.
type ModuleOutcome = {
  module: 'Parking' | 'Seats';
  ok: boolean;
  text: string;
};

function toIso(input: string): string | null {
  const m = input.trim().match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/);
  if (!m) return null;
  const [, y, mo, d, h, mi] = m;
  const dt = new Date(Number(y), Number(mo) - 1, Number(d), Number(h), Number(mi));
  return isNaN(dt.getTime()) ? null : dt.toISOString();
}

function datetimeLocalValue(offsetDays: number, hour: number, minute = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  d.setHours(hour, minute, 0, 0);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}T${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

function dateFromDateTimeLocal(value: string): string {
  return value.slice(0, 10);
}

function timeFromDateTimeLocal(value: string): string {
  return value.slice(11, 16);
}

function withDate(value: string, date: string): string {
  return `${date}T${timeFromDateTimeLocal(value) || '08:00'}`;
}

function withTime(value: string, time: string): string {
  return `${dateFromDateTimeLocal(value) || dateFromOffset(1)}T${time}`;
}

function dateFromOffset(offsetDays: number): string {
  return datetimeLocalValue(offsetDays, 0).slice(0, 10);
}

function dateOffsetFromParam(dateStr: string | null): number {
  if (!dateStr) return 1;
  const today = new Date(); today.setHours(0, 0, 0, 0);
  const target = new Date(`${dateStr}T00:00:00`);
  if (isNaN(target.getTime())) return 1;
  return Math.max(0, Math.round((target.getTime() - today.getTime()) / 86_400_000));
}

function formatPresetHours(start: string, end: string): string {
  const fmt = (t: string) => {
    const [h, m] = t.split(':').map(Number);
    return `${h % 12 || 12}:${String(m).padStart(2, '0')} ${h >= 12 ? 'PM' : 'AM'}`;
  };
  return `${fmt(start)} – ${fmt(end)}`;
}

const initialForm = (offsetDays = 1): Form => ({
  facilityId: DEMO_FACILITY_ID,
  locationId: DEMO_LOCATION_ID,
  selectedVehicleId: '',
  licensePlate: '',
  vehicleType: 'Sedan',
  isElectric: false,
  requiresAccessibleSpot: false,
  isCompanyCar: false,
  plannedArrival: datetimeLocalValue(offsetDays, 8),
  plannedDeparture: datetimeLocalValue(offsetDays, 18),
});

function describeSubmitResult(module: 'Parking' | 'Seats', res: SubmitResult): ModuleOutcome {
  const noun = module === 'Seats' ? 'seat' : 'spot';
  if (res.kind === 'accepted') {
    return {
      module,
      ok: true,
      text: res.status === 'Allocated'
        ? `Your ${noun} is allocated — no draw needed.`
        : `Request submitted — you’ll find out in the draw.`,
    };
  }
  if (res.kind === 'rejected') return { module, ok: false, text: res.reason ?? 'Request not accepted.' };
  return { module, ok: false, text: 'message' in res ? res.message : 'Submission failed.' };
}

export function NewBookingPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { hasSeats, loading: modulesLoading } = useTenantModules();
  const { dateBase, simulationActive } = useTenantDateContext();
  const quickDates = useMemo(() => nextWorkdayOptions(dateBase, 5, { relativeLabels: !simulationActive }), [dateBase, simulationActive]);
  const defaultDateApplied = useRef(false);
  const [profile, setProfile] = useState<ProfileSnapshot | null>(null);
  const [profileLoading, setProfileLoading] = useState(true);
  const [form, setForm] = useState<Form>(() => initialForm(dateOffsetFromParam(searchParams.get('date'))));
  // Module selection — parking is preselected unless the caller deep-linked the
  // seat flow (?module=seats). Seats can only be selected when tenant-enabled.
  const [wantParking, setWantParking] = useState(searchParams.get('module') !== 'seats');
  const [wantSeat, setWantSeat] = useState(searchParams.get('module') === 'seats');
  const [seatsLocation, setSeatsLocation] = useState(DEFAULT_SEATS_LOCATION);
  const [errors, setErrors] = useState<Partial<Record<keyof Form | 'modules', string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [outcomes, setOutcomes] = useState<ModuleOutcome[] | null>(null);
  const [drawStatus, setDrawStatus] = useState<DrawStatusResult | null>(null);
  const [drawStatusLoading, setDrawStatusLoading] = useState(false);
  const [dateStatuses, setDateStatuses] = useState<Record<string, DrawStatusResult>>({});
  const [dateStatusesLoading, setDateStatusesLoading] = useState(false);

  // A ?module=seats deep link on a tenant without Seats (or before modules resolve
  // to parking-only) falls back to a plain parking request instead of a dead form.
  useEffect(() => {
    if (modulesLoading || hasSeats) return;
    if (wantSeat) setWantSeat(false);
    if (!wantParking) setWantParking(true);
  }, [modulesLoading, hasSeats, wantSeat, wantParking]);

  useEffect(() => {
    if (defaultDateApplied.current) return;
    if (searchParams.get('date')) return;
    const defaultDate = (isWorkday(dateBase) ? quickDates[1] : quickDates[0])?.date;
    if (!defaultDate) return;
    defaultDateApplied.current = true;
    setForm(f => ({
      ...f,
      plannedArrival: withDate(f.plannedArrival, defaultDate),
      plannedDeparture: withDate(f.plannedDeparture, defaultDate),
    }));
  }, [quickDates, searchParams]);

  useEffect(() => {
    fetchProfileSnapshot({ apiBaseUrl, bearerToken }).then((res) => {
      if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (res.kind === 'ok') {
        setProfile(res.data);
        const defaultVehicle = res.data.vehicles.find(v => v.isActive && v.isDefault);
        if (defaultVehicle) {
          setForm((f) => ({
            ...f,
            selectedVehicleId: defaultVehicle.vehicleId,
            licensePlate: defaultVehicle.licensePlate,
            vehicleType: defaultVehicle.vehicleType,
            isElectric: defaultVehicle.isElectric,
          }));
        }
      }
      setProfileLoading(false);
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  // Reuse the seats location the tenant already allocates at when the employee
  // has seat history; otherwise keep the showcase seats location.
  useEffect(() => {
    if (!hasSeats) return;
    let cancelled = false;
    void fetchBookings({ apiBaseUrl, bearerToken }).then((r) => {
      if (cancelled || r.kind !== 'ok') return;
      const known = r.items.find((i) => i.resourceType === 'Seats' && i.locationId)?.locationId;
      if (known) setSeatsLocation(known);
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, hasSeats]);

  useEffect(() => {
    const arrivalMatch = form.plannedArrival.match(/^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})$/);
    const departureMatch = form.plannedDeparture.match(/^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})$/);
    if (!arrivalMatch || !departureMatch || arrivalMatch[1] !== departureMatch[1]) {
      setDrawStatus(null);
      return;
    }

    let cancelled = false;
    setDrawStatusLoading(true);
    fetchDrawStatus({ apiBaseUrl, bearerToken }, {
      date: arrivalMatch[1],
      locationId: form.locationId.trim() || DEMO_LOCATION_ID,
      timeSlotStart: `${arrivalMatch[2]}:00`,
      timeSlotEnd: `${departureMatch[2]}:00`,
    }).then((res) => {
      if (cancelled) return;
      setDrawStatus(res);
      setDrawStatusLoading(false);
    });

    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, form.locationId, form.plannedArrival, form.plannedDeparture]);

  useEffect(() => {
    const arrivalTime = timeFromDateTimeLocal(form.plannedArrival);
    const departureTime = timeFromDateTimeLocal(form.plannedDeparture);
    if (!arrivalTime || !departureTime) return;

    let cancelled = false;
    setDateStatusesLoading(true);
    Promise.all(quickDates.map(async (option) => {
      const date = option.date;
      const status = await fetchDrawStatus({ apiBaseUrl, bearerToken }, {
        date,
        locationId: form.locationId.trim() || DEMO_LOCATION_ID,
        timeSlotStart: `${arrivalTime}:00`,
        timeSlotEnd: `${departureTime}:00`,
      });
      return [date, status] as const;
    })).then((entries) => {
      if (cancelled) return;
      setDateStatuses(Object.fromEntries(entries));
      setDateStatusesLoading(false);
    });

    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, form.locationId, form.plannedArrival, form.plannedDeparture, quickDates]);

  function set<K extends keyof Form>(k: K, v: Form[K]) {
    setForm((f) => ({ ...f, [k]: v }));
    setErrors((e) => ({ ...e, [k]: undefined }));
  }

  function selectVehicle(vehicleId: string) {
    const vehicle = profile?.vehicles.find((v) => v.vehicleId === vehicleId);
    if (vehicle) {
      setForm((f) => ({
        ...f,
        selectedVehicleId: vehicleId,
        licensePlate: vehicle.licensePlate,
        vehicleType: vehicle.vehicleType,
        isElectric: vehicle.isElectric,
      }));
      setErrors((e) => ({ ...e, licensePlate: undefined, vehicleType: undefined }));
    }
  }

  function setRequestDate(date: string) {
    setForm((f) => ({
      ...f,
      plannedArrival: withDate(f.plannedArrival, date),
      plannedDeparture: withDate(f.plannedDeparture, date),
    }));
    setErrors((e) => ({ ...e, plannedArrival: undefined, plannedDeparture: undefined }));
  }

  function setTimePreset(start: string, end: string) {
    setForm((f) => ({
      ...f,
      plannedArrival: withTime(f.plannedArrival, start),
      plannedDeparture: withTime(f.plannedDeparture, end),
    }));
    setErrors((e) => ({ ...e, plannedArrival: undefined, plannedDeparture: undefined }));
  }

  const activePresetKey = TIME_PRESETS.find(p =>
    timeFromDateTimeLocal(form.plannedArrival) === p.start && timeFromDateTimeLocal(form.plannedDeparture) === p.end
  )?.key ?? null;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!wantParking && !wantSeat) errs.modules = 'Select at least one request to submit.';
    if (wantParking && !form.facilityId.trim()) errs.facilityId = 'Required';
    if (wantParking && !form.licensePlate.trim()) errs.licensePlate = 'Select a vehicle or enter license plate';
    const arrival = toIso(form.plannedArrival);
    const departure = toIso(form.plannedDeparture);
    if (!arrival) errs.plannedArrival = 'Use YYYY-MM-DDTHH:MM';
    if (!departure) errs.plannedDeparture = 'Use YYYY-MM-DDTHH:MM';
    if (arrival && departure && departure <= arrival) errs.plannedDeparture = 'Must be after arrival';
    if (wantParking && drawStatus?.kind === 'ok' && !drawStatus.canRequest) errs.plannedArrival = drawStatus.cannotRequestReason || 'Requests are closed for this time.';
    if (Object.keys(errs).length) { setErrors(errs); return; }

    setSubmitting(true);
    setOutcomes(null);

    // Each selected module submits independently — partial success is the default.
    const results: ModuleOutcome[] = [];
    if (wantParking) {
      const res = await submitBooking({ apiBaseUrl, bearerToken }, {
        facilityId: form.facilityId.trim(),
        locationId: form.locationId.trim() || null,
        licensePlate: form.licensePlate.trim(),
        vehicleType: form.vehicleType,
        isElectric: form.isElectric,
        requiresAccessibleSpot: form.requiresAccessibleSpot,
        isCompanyCar: form.isCompanyCar,
        plannedArrivalTime: arrival!,
        plannedDepartureTime: departure!,
      });
      if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      results.push(describeSubmitResult('Parking', res));
    }
    if (wantSeat && hasSeats) {
      const res = await submitBooking({ apiBaseUrl, bearerToken }, {
        facilityId: DEMO_FACILITY_ID,
        locationId: seatsLocation,
        resourceType: 'Seats',
        // Vehicle fields are ignored for seats.
        licensePlate: 'N/A',
        vehicleType: 'Sedan',
        isElectric: false,
        requiresAccessibleSpot: false,
        isCompanyCar: false,
        plannedArrivalTime: arrival!,
        plannedDepartureTime: departure!,
      });
      if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      results.push(describeSubmitResult('Seats', res));
    }

    setSubmitting(false);
    setOutcomes(results);
  }

  const selectedDate = dateFromDateTimeLocal(form.plannedArrival);
  const selectedDateLabel = quickDates.find(q => q.date === selectedDate)?.label ?? selectedDate;

  return (
    <div style={{ maxWidth: 560 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24 }}>
        <button onClick={() => navigate('/bookings')} style={backBtn}>← Back</button>
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Request</h2>
      </div>

      {profileLoading ? (
        <p style={{ color: '#6b7280', fontSize: 14 }}>Loading…</p>
      ) : (
        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* 1 — date first */}
          <Field label="Date *" error={errors.plannedArrival}>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
              {quickDates.map((option) => {
                const status = dateStatuses[option.date];
                // Parking window state must not block seat-only selection.
                const closed = wantParking && status?.kind === 'ok' && !status.canRequest;
                const selected = selectedDate === option.date;
                return (
                  <button
                    key={option.date}
                    type="button"
                    onClick={() => setRequestDate(option.date)}
                    disabled={closed || dateStatusesLoading}
                    style={{
                      padding: '6px 12px',
                      borderRadius: 6,
                      border: `2px solid ${selected ? '#1d4ed8' : '#e5e7eb'}`,
                      background: selected ? '#1d4ed8' : '#fff',
                      color: closed ? '#9ca3af' : selected ? '#fff' : '#374151',
                      fontWeight: 600,
                      cursor: closed || dateStatusesLoading ? 'not-allowed' : 'pointer',
                      fontSize: 13,
                      opacity: closed ? 0.6 : 1,
                    }}
                  >
                    {option.label}{closed ? ' - closed' : ''}
                  </button>
                );
              })}
            </div>
            {/* Secondary escape hatch for uncommon dates outside the quick choices. */}
            <input
              type="date"
              value={selectedDate}
              min={toLocalDateString(dateBase)}
              onChange={(e) => setRequestDate(e.target.value)}
              style={{ ...inputStyle, marginTop: 8 }}
            />
            {dateStatusesLoading ? <span style={{ fontSize: 12, color: '#6b7280' }}>Checking available dates...</span> : null}
          </Field>

          {/* 2 — time presets shown as actual hours */}
          <Field label="Time *" error={errors.plannedDeparture}>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
              {TIME_PRESETS.map((preset) => {
                const selected = activePresetKey === preset.key;
                return (
                  <button
                    key={preset.key}
                    type="button"
                    onClick={() => setTimePreset(preset.start, preset.end)}
                    style={{
                      padding: '6px 12px',
                      borderRadius: 6,
                      border: `2px solid ${selected ? '#1d4ed8' : '#e5e7eb'}`,
                      background: selected ? '#1d4ed8' : '#fff',
                      color: selected ? '#fff' : '#374151',
                      fontWeight: 600,
                      cursor: 'pointer',
                      fontSize: 13,
                    }}
                  >
                    {preset.name} · {formatPresetHours(preset.start, preset.end)}
                  </button>
                );
              })}
            </div>
            {/* Custom times remain available; editing them simply deselects the presets. */}
            <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
              <label style={{ flex: 1, fontSize: 12, color: '#6b7280' }}>Arrival
                <input type="time" value={timeFromDateTimeLocal(form.plannedArrival)} onChange={(e) => set('plannedArrival', withTime(form.plannedArrival, e.target.value))} style={inputStyle} />
              </label>
              <label style={{ flex: 1, fontSize: 12, color: '#6b7280' }}>Departure
                <input type="time" value={timeFromDateTimeLocal(form.plannedDeparture)} onChange={(e) => set('plannedDeparture', withTime(form.plannedDeparture, e.target.value))} style={inputStyle} />
              </label>
            </div>
          </Field>

          {/* 3 — module options for the selected date/time */}
          <Field label={`For ${selectedDateLabel}, ${formatPresetHours(timeFromDateTimeLocal(form.plannedArrival), timeFromDateTimeLocal(form.plannedDeparture))}`} error={errors.modules}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {/* Parking module card */}
              <div style={moduleCardStyle(wantParking)}>
                <label style={moduleHeadStyle}>
                  <input type="checkbox" checked={wantParking} onChange={(e) => { setWantParking(e.target.checked); setErrors((er) => ({ ...er, modules: undefined })); }} />
                  <ModuleBadge resourceType="Parking" />
                  <span style={{ fontWeight: 600, fontSize: 14 }}>Parking spot</span>
                </label>

                {wantParking && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 10 }}>
                    <Field label="Facility *" error={errors.facilityId}>
                      <select value={form.facilityId} onChange={(e) => set('facilityId', e.target.value)} style={inputStyle}>
                        <option value="">Select a facility…</option>
                        <option value={DEMO_FACILITY_ID}>Headquarters</option>
                      </select>
                    </Field>
                    <Field label="Location (optional)" error={errors.locationId}>
                      <select value={form.locationId} onChange={(e) => set('locationId', e.target.value)} style={inputStyle}>
                        <option value="">Any location</option>
                        <option value={DEMO_LOCATION_ID}>Prague</option>
                      </select>
                    </Field>

                    {profile && profile.vehicles.filter(v => v.isActive).length > 0 ? (
                      <Field label="Vehicle *" error={errors.licensePlate}>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                          {profile.vehicles.filter(v => v.isActive).map((v) => (
                            <button
                              key={v.vehicleId}
                              type="button"
                              onClick={() => selectVehicle(v.vehicleId)}
                              style={{
                                padding: '10px 14px',
                                borderRadius: 6,
                                border: `2px solid ${form.selectedVehicleId === v.vehicleId ? '#1d4ed8' : '#e5e7eb'}`,
                                background: form.selectedVehicleId === v.vehicleId ? '#eff6ff' : '#fff',
                                color: '#111827',
                                cursor: 'pointer',
                                textAlign: 'left',
                                fontSize: 14,
                              }}
                            >
                              <div style={{ fontWeight: 600 }}>{v.licensePlate}</div>
                              <div style={{ fontSize: 13, color: '#6b7280' }}>
                                {v.vehicleType} · {v.isElectric ? 'Electric' : 'Standard'}
                              </div>
                            </button>
                          ))}
                        </div>
                      </Field>
                    ) : (
                      <>
                        <Field label="License plate *" error={errors.licensePlate}>
                          <input value={form.licensePlate} onChange={(e) => set('licensePlate', e.target.value)} placeholder="e.g. ABC-123" style={inputStyle} />
                          <p style={{ margin: '4px 0 0', fontSize: 12, color: '#6b7280' }}>No vehicles in profile. Add vehicles in Profile page to speed up spot requests.</p>
                        </Field>
                        <Field label="Vehicle type *" error={errors.vehicleType}>
                          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                            {VEHICLE_TYPES.map((vt) => (
                              <button
                                key={vt} type="button" onClick={() => set('vehicleType', vt)}
                                style={{ padding: '6px 14px', borderRadius: 6, border: `2px solid ${form.vehicleType === vt ? '#1d4ed8' : '#e5e7eb'}`, background: form.vehicleType === vt ? '#1d4ed8' : '#fff', color: form.vehicleType === vt ? '#fff' : '#374151', fontWeight: 500, cursor: 'pointer', fontSize: 13 }}
                              >
                                {vt}
                              </button>
                            ))}
                          </div>
                        </Field>
                      </>
                    )}

                    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                      {form.selectedVehicleId ? null : (
                        <label style={checkboxRow}>
                          <input type="checkbox" checked={form.isElectric} onChange={(e) => set('isElectric', e.target.checked)} />
                          Electric vehicle
                        </label>
                      )}
                      <label style={checkboxRow}>
                        <input type="checkbox" checked={form.requiresAccessibleSpot} onChange={(e) => set('requiresAccessibleSpot', e.target.checked)} />
                        Requires accessible spot
                      </label>
                      <label style={checkboxRow}>
                        <input type="checkbox" checked={form.isCompanyCar} onChange={(e) => set('isCompanyCar', e.target.checked)} />
                        Company car
                      </label>
                    </div>

                    {drawStatusLoading ? (
                      <div style={{ fontSize: 13, color: '#6b7280' }}>Checking request window…</div>
                    ) : drawStatus?.kind === 'ok' && !drawStatus.canRequest ? (
                      <div style={{ padding: '10px 14px', borderRadius: 8, background: '#f9fafb', border: '1px solid #e5e7eb', color: '#6b7280', fontSize: 13 }}>
                        {drawStatus.cannotRequestReason || drawStatus.safeMessage || 'Requests are closed for this time.'}
                      </div>
                    ) : null}
                  </div>
                )}
              </div>

              {/* Seat module card — only for tenants that enable Seats. */}
              {hasSeats && (
                <div style={moduleCardStyle(wantSeat)}>
                  <label style={moduleHeadStyle}>
                    <input type="checkbox" checked={wantSeat} onChange={(e) => { setWantSeat(e.target.checked); setErrors((er) => ({ ...er, modules: undefined })); }} />
                    <ModuleBadge resourceType="Seats" />
                    <span style={{ fontWeight: 600, fontSize: 14 }}>Team seat</span>
                  </label>
                  {wantSeat && (
                    <p style={{ margin: '8px 0 0', fontSize: 13, color: '#6b7280' }}>
                      A shared team seat for the selected date and time. Seats are allocated by the same fair draw as parking; when a day is popular a small waitlist forms.
                    </p>
                  )}
                </div>
              )}
            </div>
          </Field>

          {/* Per-module results — partial success is expected and reported clearly. */}
          {outcomes && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {outcomes.map((o) => (
                <div key={o.module} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '10px 14px', borderRadius: 8, background: o.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${o.ok ? '#bbf7d0' : '#fecaca'}`, color: o.ok ? '#166534' : '#b91c1c', fontSize: 13 }}>
                  <ModuleBadge resourceType={o.module} />
                  <span>{o.text}</span>
                </div>
              ))}
              {outcomes.some(o => o.ok) && (
                <button type="button" onClick={() => navigate('/bookings')} style={{ ...backBtn, alignSelf: 'flex-start' }}>
                  View My Reservations →
                </button>
              )}
            </div>
          )}

          <button
            type="submit"
            disabled={submitting || (wantParking && (drawStatusLoading || (drawStatus?.kind === 'ok' && !drawStatus.canRequest)))}
            style={{ ...primaryBtn, opacity: submitting || (wantParking && (drawStatusLoading || (drawStatus?.kind === 'ok' && !drawStatus.canRequest))) ? 0.6 : 1 }}
          >
            {submitting ? 'Submitting…' : wantParking && wantSeat ? 'Submit selected requests' : 'Submit request'}
          </button>
        </form>
      )}
    </div>
  );
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <label style={{ fontSize: 13, fontWeight: 600, color: '#111827' }}>{label}</label>
      {children}
      {error ? <span style={{ fontSize: 12, color: '#b91c1c' }}>{error}</span> : null}
    </div>
  );
}

function moduleCardStyle(selected: boolean): React.CSSProperties {
  return {
    border: `2px solid ${selected ? '#1d4ed8' : '#e5e7eb'}`,
    borderRadius: 8,
    padding: '12px 14px',
    background: '#fff',
  };
}

const moduleHeadStyle: React.CSSProperties = { display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', fontSize: 14 };
const checkboxRow: React.CSSProperties = { display: 'flex', alignItems: 'center', gap: 10, fontSize: 14, cursor: 'pointer' };
const inputStyle: React.CSSProperties = { border: '1px solid #e5e7eb', borderRadius: 6, padding: '8px 12px', fontSize: 14, color: '#111827', background: '#fff', width: '100%', boxSizing: 'border-box' };
const primaryBtn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 8, padding: '10px 0', fontSize: 14, fontWeight: 700, cursor: 'pointer' };
const backBtn: React.CSSProperties = { background: 'none', border: 'none', color: '#1d4ed8', fontSize: 14, cursor: 'pointer', padding: 0 };
