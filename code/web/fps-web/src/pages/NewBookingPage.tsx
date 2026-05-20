import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { submitBooking } from '../api/bookings';

const VEHICLE_TYPES = ['Compact', 'Sedan', 'SUV', 'Van', 'Truck', 'Motorcycle'] as const;

type Form = {
  facilityId: string;
  locationId: string;
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

export function NewBookingPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState<Form>({
    facilityId: '', locationId: '', licensePlate: '', vehicleType: 'Sedan',
    isElectric: false, requiresAccessibleSpot: false, isCompanyCar: false,
    plannedArrival: '', plannedDeparture: '',
  });
  const [errors, setErrors] = useState<Partial<Record<keyof Form, string>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [feedback, setFeedback] = useState<{ ok: boolean; text: string } | null>(null);

  function set<K extends keyof Form>(k: K, v: Form[K]) {
    setForm((f) => ({ ...f, [k]: v }));
    setErrors((e) => ({ ...e, [k]: undefined }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const errs: typeof errors = {};
    if (!form.facilityId.trim()) errs.facilityId = 'Required';
    if (!form.licensePlate.trim()) errs.licensePlate = 'Required';
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
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>New Parking Request</h2>
      </div>

      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <Field label="Facility ID *" error={errors.facilityId}>
          <input value={form.facilityId} onChange={(e) => set('facilityId', e.target.value)} placeholder="e.g. FAC-001" style={inputStyle} />
        </Field>
        <Field label="Location ID (optional)" error={errors.locationId}>
          <input value={form.locationId} onChange={(e) => set('locationId', e.target.value)} placeholder="Optional" style={inputStyle} />
        </Field>
        <Field label="License plate *" error={errors.licensePlate}>
          <input value={form.licensePlate} onChange={(e) => set('licensePlate', e.target.value)} placeholder="e.g. ABC-123" style={inputStyle} />
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
        <Field label="Planned arrival *" error={errors.plannedArrival}>
          <input type="datetime-local" value={form.plannedArrival} onChange={(e) => set('plannedArrival', e.target.value)} style={inputStyle} />
        </Field>
        <Field label="Planned departure *" error={errors.plannedDeparture}>
          <input type="datetime-local" value={form.plannedDeparture} onChange={(e) => set('plannedDeparture', e.target.value)} style={inputStyle} />
        </Field>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {([['isElectric', 'Electric vehicle'], ['requiresAccessibleSpot', 'Requires accessible spot'], ['isCompanyCar', 'Company car']] as [keyof Form, string][]).map(([k, label]) => (
            <label key={k} style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 14, cursor: 'pointer' }}>
              <input type="checkbox" checked={form[k] as boolean} onChange={(e) => set(k, e.target.checked)} />
              {label}
            </label>
          ))}
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
