import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { submitBooking } from '../api/bookings';
import { fetchProfileSnapshot, type ProfileSnapshot } from '../api/profile';

const VEHICLE_TYPES = ['Compact', 'Sedan', 'SUV', 'Van', 'Truck', 'Motorcycle'] as const;
const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';
const DEMO_LOCATION_ID = 'LOC-MAIN';

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

const initialForm = (): Form => ({
  facilityId: DEMO_FACILITY_ID,
  locationId: DEMO_LOCATION_ID,
  selectedVehicleId: '',
  licensePlate: '',
  vehicleType: 'Sedan',
  isElectric: false,
  requiresAccessibleSpot: false,
  isCompanyCar: false,
  plannedArrival: datetimeLocalValue(1, 8),
  plannedDeparture: datetimeLocalValue(1, 18),
});

export function NewBookingPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [profile, setProfile] = useState<ProfileSnapshot | null>(null);
  const [profileLoading, setProfileLoading] = useState(true);
  const [form, setForm] = useState<Form>(() => initialForm());
  const [errors, setErrors] = useState<Partial<Record<keyof Form, string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [feedback, setFeedback] = useState<{ ok: boolean; text: string } | null>(null);

  useEffect(() => {
    fetchProfileSnapshot({ apiBaseUrl, bearerToken }).then((res) => {
      if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (res.kind === 'ok') setProfile(res.data);
      setProfileLoading(false);
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

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

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!form.facilityId.trim()) errs.facilityId = 'Required';
    if (!form.licensePlate.trim()) errs.licensePlate = 'Select a vehicle or enter license plate';
    const arrival = toIso(form.plannedArrival);
    const departure = toIso(form.plannedDeparture);
    if (!arrival) errs.plannedArrival = 'Use YYYY-MM-DDTHH:MM';
    if (!departure) errs.plannedDeparture = 'Use YYYY-MM-DDTHH:MM';
    if (arrival && departure && departure <= arrival) errs.plannedDeparture = 'Must be after arrival';
    if (Object.keys(errs).length) { setErrors(errs); return; }

    setSubmitting(true);
    setFeedback(null);
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
    setSubmitting(false);

    if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (res.kind === 'accepted') { navigate('/bookings'); return; }
    if (res.kind === 'rejected') {
      setFeedback({ ok: false, text: res.reason ?? 'Request not accepted.' });
    } else {
      setFeedback({ ok: false, text: 'message' in res ? res.message : 'Submission failed.' });
    }
  }

  return (
    <div style={{ maxWidth: 560 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24 }}>
        <button onClick={() => navigate('/bookings')} style={backBtn}>← Back</button>
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Request a spot</h2>
      </div>

      {profileLoading ? (
        <p style={{ color: '#6b7280', fontSize: 14 }}>Loading vehicles…</p>
      ) : (
        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Field label="Facility *" error={errors.facilityId}>
            <select
              value={form.facilityId}
              onChange={(e) => set('facilityId', e.target.value)}
              style={inputStyle}
            >
              <option value="">Select a facility…</option>
              <option value={DEMO_FACILITY_ID}>Main building</option>
            </select>
          </Field>
          <Field label="Location (optional)" error={errors.locationId}>
            <select
              value={form.locationId}
              onChange={(e) => set('locationId', e.target.value)}
              style={inputStyle}
            >
              <option value="">Any location</option>
              <option value={DEMO_LOCATION_ID}>Main office</option>
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

          <Field label="Planned arrival *" error={errors.plannedArrival}>
            <input type="datetime-local" value={form.plannedArrival} onChange={(e) => set('plannedArrival', e.target.value)} style={inputStyle} />
          </Field>
          <Field label="Planned departure *" error={errors.plannedDeparture}>
            <input type="datetime-local" value={form.plannedDeparture} onChange={(e) => set('plannedDeparture', e.target.value)} style={inputStyle} />
          </Field>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {form.selectedVehicleId ? null : (
              <label style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 14, cursor: 'pointer' }}>
                <input type="checkbox" checked={form.isElectric} onChange={(e) => set('isElectric', e.target.checked)} />
                Electric vehicle
              </label>
            )}
            <label style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 14, cursor: 'pointer' }}>
              <input type="checkbox" checked={form.requiresAccessibleSpot} onChange={(e) => set('requiresAccessibleSpot', e.target.checked)} />
              Requires accessible spot
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 14, cursor: 'pointer' }}>
              <input type="checkbox" checked={form.isCompanyCar} onChange={(e) => set('isCompanyCar', e.target.checked)} />
              Company car
            </label>
          </div>

          {feedback ? (
            <div style={{ padding: '10px 14px', borderRadius: 8, background: feedback.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${feedback.ok ? '#bbf7d0' : '#fecaca'}`, color: feedback.ok ? '#166534' : '#b91c1c', fontSize: 13 }}>
              {feedback.text}
            </div>
          ) : null}

          <button type="submit" disabled={submitting} style={{ ...primaryBtn, opacity: submitting ? 0.6 : 1 }}>
            {submitting ? 'Submitting…' : 'Submit request'}
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

const inputStyle: React.CSSProperties = { border: '1px solid #e5e7eb', borderRadius: 6, padding: '8px 12px', fontSize: 14, color: '#111827', background: '#fff', width: '100%', boxSizing: 'border-box' };
const primaryBtn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 8, padding: '10px 0', fontSize: 14, fontWeight: 700, cursor: 'pointer' };
const backBtn: React.CSSProperties = { background: 'none', border: 'none', color: '#1d4ed8', fontSize: 14, cursor: 'pointer', padding: 0 };
