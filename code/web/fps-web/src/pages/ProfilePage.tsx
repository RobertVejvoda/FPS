import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  addVehicle,
  fetchProfileSnapshot,
  removeVehicle,
  setDefaultVehicle,
  type ProfileSnapshot,
  type VehicleSnapshot,
} from '../api/profile';
import { fetchMe, type MeResponse } from '../api/client';
import { formatRoles } from '../displayLabels';

const VEHICLE_TYPES = ['Compact', 'Sedan', 'SUV', 'Van', 'Truck', 'Motorcycle'] as const;

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; me: MeResponse; profile: ProfileSnapshot }
  | { kind: 'error'; message: string };

type AddForm = { licensePlate: string; vehicleType: string; isElectric: boolean };
const emptyForm = (): AddForm => ({ licensePlate: '', vehicleType: 'Sedan', isElectric: false });

export function ProfilePage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [addForm, setAddForm] = useState<AddForm>(emptyForm());
  const [addOpen, setAddOpen] = useState(false);
  const [addError, setAddError] = useState('');
  const [actionError, setActionError] = useState('');
  const [busy, setBusy] = useState(false);

  const cfg = { apiBaseUrl, bearerToken };

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      fetchMe({ apiBaseUrl, bearerToken }),
      fetchProfileSnapshot({ apiBaseUrl, bearerToken }),
    ]).then(([meResult, profileResult]) => {
      if (cancelled) return;
      if (meResult.kind === 'unauthenticated' || profileResult.kind === 'unauthenticated') {
        clear(); navigate('/session'); return;
      }
      if (meResult.kind !== 'ok') {
        setState({ kind: 'error', message: 'message' in meResult ? meResult.message : 'Failed to load identity.' });
        return;
      }
      if (profileResult.kind !== 'ok') {
        setState({ kind: 'error', message: 'message' in profileResult ? profileResult.message : 'Failed to load profile.' });
        return;
      }
      setState({ kind: 'ok', me: meResult.data, profile: profileResult.data });
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  async function reloadProfile() {
    const res = await fetchProfileSnapshot(cfg);
    if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (res.kind === 'ok') setState(s => s.kind === 'ok' ? { ...s, profile: res.data } : s);
  }

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    if (!addForm.licensePlate.trim()) { setAddError('License plate is required.'); return; }
    setBusy(true);
    setAddError('');
    const res = await addVehicle(cfg, {
      licensePlate: addForm.licensePlate.trim(),
      vehicleType: addForm.vehicleType,
      isElectric: addForm.isElectric,
    });
    setBusy(false);
    if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (res.kind !== 'ok') { setAddError('message' in res ? res.message : 'Could not add vehicle.'); return; }
    setAddForm(emptyForm());
    setAddOpen(false);
    await reloadProfile();
  }

  async function handleRemove(vehicleId: string) {
    if (!confirm('Remove this vehicle from your profile?')) return;
    setBusy(true);
    setActionError('');
    const res = await removeVehicle(cfg, vehicleId);
    setBusy(false);
    if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (res.kind !== 'ok') { setActionError('Could not remove vehicle. Please try again.'); return; }
    await reloadProfile();
  }

  async function handleSetDefault(vehicleId: string) {
    setBusy(true);
    setActionError('');
    const res = await setDefaultVehicle(cfg, vehicleId);
    setBusy(false);
    if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (res.kind !== 'ok') { setActionError('Could not update default vehicle. Please try again.'); return; }
    await reloadProfile();
  }

  if (state.kind === 'loading') return <p style={muted}>Loading profile…</p>;
  if (state.kind === 'error') return <p style={{ color: '#b91c1c' }}>{state.message}</p>;

  const { me, profile } = state;
  const activeVehicles = profile.vehicles.filter(v => v.isActive);

  return (
    <div style={page}>
      <h2 style={heading}>My Profile</h2>

      <section style={card}>
        <h3 style={cardTitle}>Account</h3>
        <Row label="Roles" value={formatRoles(me.roles)} />
      </section>

      <section style={card}>
        <h3 style={cardTitle}>Spot Eligibility</h3>
        <Row label="Profile status" value={profile.profileStatus} />
        <Row label="Spot eligible" value={profile.parkingEligible ? 'Yes' : 'No'} />
        <Row label="Company car" value={profile.hasCompanyCar ? 'Yes' : 'No'} />
        <Row label="Accessibility eligible" value={profile.accessibilityEligible ? 'Yes' : 'No'} />
        <Row label="Reserved space eligible" value={profile.reservedSpaceEligible ? 'Yes' : 'No'} />
        <Row label="Snapshot version" value={profile.snapshotVersion} />
      </section>

      <section style={card}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
          <h3 style={{ ...cardTitle, margin: 0 }}>My Vehicles</h3>
          <button
            onClick={() => { setAddOpen(o => !o); setAddError(''); }}
            style={secondaryBtn}
          >
            {addOpen ? 'Cancel' : '+ Add vehicle'}
          </button>
        </div>

        {addOpen && (
          <form onSubmit={handleAdd} style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: '12px 0', borderBottom: '1px solid #e5e7eb' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={fieldLabel}>License plate *</label>
              <input
                value={addForm.licensePlate}
                onChange={e => setAddForm(f => ({ ...f, licensePlate: e.target.value }))}
                placeholder="e.g. ABC-123"
                style={inputStyle}
              />
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={fieldLabel}>Vehicle type *</label>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {VEHICLE_TYPES.map(vt => (
                  <button
                    key={vt} type="button"
                    onClick={() => setAddForm(f => ({ ...f, vehicleType: vt }))}
                    style={{
                      padding: '5px 12px', borderRadius: 6, fontSize: 13, fontWeight: 500, cursor: 'pointer',
                      border: `2px solid ${addForm.vehicleType === vt ? '#1d4ed8' : '#e5e7eb'}`,
                      background: addForm.vehicleType === vt ? '#1d4ed8' : '#fff',
                      color: addForm.vehicleType === vt ? '#fff' : '#374151',
                    }}
                  >{vt}</button>
                ))}
              </div>
            </div>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer' }}>
              <input type="checkbox" checked={addForm.isElectric} onChange={e => setAddForm(f => ({ ...f, isElectric: e.target.checked }))} />
              Electric vehicle
            </label>
            {addError && <p style={{ margin: 0, fontSize: 13, color: '#b91c1c' }}>{addError}</p>}
            <button type="submit" disabled={busy} style={{ ...primaryBtn, opacity: busy ? 0.6 : 1 }}>
              {busy ? 'Adding…' : 'Add vehicle'}
            </button>
          </form>
        )}

        {actionError && <p style={{ margin: '4px 0 0', fontSize: 13, color: '#b91c1c' }}>{actionError}</p>}

        {activeVehicles.length === 0 && !addOpen && (
          <p style={muted}>No vehicles linked to your profile. Add one to speed up spot requests.</p>
        )}

        {activeVehicles.map(v => (
          <VehicleCard
            key={v.vehicleId}
            vehicle={v}
            busy={busy}
            onSetDefault={() => handleSetDefault(v.vehicleId)}
            onRemove={() => handleRemove(v.vehicleId)}
          />
        ))}
      </section>
    </div>
  );
}

function VehicleCard({ vehicle, busy, onSetDefault, onRemove }: {
  vehicle: VehicleSnapshot;
  busy: boolean;
  onSetDefault: () => void;
  onRemove: () => void;
}) {
  return (
    <div style={{ borderTop: '1px solid #e5e7eb', paddingTop: 10, marginTop: 10 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 2 }}>
        <span style={{ fontWeight: 600, fontSize: 14 }}>{vehicle.licensePlate || vehicle.vehicleType}</span>
        {vehicle.isDefault && (
          <span style={{ fontSize: 11, fontWeight: 700, color: '#1d4ed8', background: '#eff6ff', borderRadius: 4, padding: '1px 6px', textTransform: 'uppercase' }}>
            Default
          </span>
        )}
      </div>
      <div style={{ ...muted, marginBottom: 8 }}>{vehicle.vehicleType} · {vehicle.isElectric ? 'Electric' : 'Standard'}</div>
      <div style={{ display: 'flex', gap: 8 }}>
        {!vehicle.isDefault && (
          <button onClick={onSetDefault} disabled={busy} style={ghostBtn}>Set as default</button>
        )}
        <button onClick={onRemove} disabled={busy} style={{ ...ghostBtn, color: '#b91c1c', borderColor: '#fecaca' }}>Remove</button>
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 16, padding: '4px 0' }}>
      <span style={muted}>{label}</span>
      <span style={{ fontWeight: 500, textAlign: 'right' }}>{value}</span>
    </div>
  );
}

const page: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 16 };
const heading: React.CSSProperties = { margin: 0, fontSize: 20, fontWeight: 700 };
const card: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 4 };
const cardTitle: React.CSSProperties = { margin: '0 0 8px', fontSize: 15, fontWeight: 700 };
const muted: React.CSSProperties = { color: '#6b7280', fontSize: 14 };
const inputStyle: React.CSSProperties = { border: '1px solid #e5e7eb', borderRadius: 6, padding: '7px 10px', fontSize: 14, color: '#111827', background: '#fff', width: '100%', boxSizing: 'border-box' };
const fieldLabel: React.CSSProperties = { fontSize: 13, fontWeight: 600, color: '#111827' };
const primaryBtn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 13, fontWeight: 600, cursor: 'pointer' };
const secondaryBtn: React.CSSProperties = { background: '#fff', color: '#1d4ed8', border: '1px solid #1d4ed8', borderRadius: 6, padding: '5px 12px', fontSize: 13, fontWeight: 600, cursor: 'pointer' };
const ghostBtn: React.CSSProperties = { background: '#fff', color: '#374151', border: '1px solid #e5e7eb', borderRadius: 6, padding: '4px 10px', fontSize: 12, fontWeight: 500, cursor: 'pointer' };
