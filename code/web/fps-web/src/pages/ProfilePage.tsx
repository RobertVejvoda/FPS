import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { formatRoles } from '../auth/roles';
import {
  addVehicle,
  fetchProfileSnapshot,
  removeVehicle,
  setDefaultVehicle,
  type ProfileSnapshot,
  type VehicleSnapshot,
} from '../api/profile';
import { fetchMe, type MeResponse } from '../api/client';
import { displayLocation } from '../displayLabels';
import { t, tDynamic } from '../i18n';

// Local-demo defaults. Until Profile/ProfileSnapshot carries a home
// location/facility per user (out of scope for #477), HR/admin and
// employees alike share the demo facility "Headquarters" — so the
// Profile page no longer reads as if facility data is simply missing.
const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';
const DEMO_LOCATION_ID = 'Prague';

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
        setState({ kind: 'error', message: 'message' in meResult ? meResult.message : t('profile.error.loadIdentity') });
        return;
      }
      if (profileResult.kind !== 'ok') {
        setState({ kind: 'error', message: 'message' in profileResult ? profileResult.message : t('profile.error.loadProfile') });
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
    if (!addForm.licensePlate.trim()) { setAddError(t('profile.vehicles.addError.required')); return; }
    setBusy(true);
    setAddError('');
    const res = await addVehicle(cfg, {
      licensePlate: addForm.licensePlate.trim(),
      vehicleType: addForm.vehicleType,
      isElectric: addForm.isElectric,
    });
    setBusy(false);
    if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (res.kind !== 'ok') { setAddError('message' in res ? res.message : t('profile.vehicles.addError.generic')); return; }
    setAddForm(emptyForm());
    setAddOpen(false);
    await reloadProfile();
  }

  async function handleRemove(vehicleId: string) {
    if (!confirm(t('profile.vehicles.removeConfirm'))) return;
    setBusy(true);
    setActionError('');
    const res = await removeVehicle(cfg, vehicleId);
    setBusy(false);
    if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (res.kind !== 'ok') { setActionError(t('profile.vehicles.removeError')); return; }
    await reloadProfile();
  }

  async function handleSetDefault(vehicleId: string) {
    setBusy(true);
    setActionError('');
    const res = await setDefaultVehicle(cfg, vehicleId);
    setBusy(false);
    if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (res.kind !== 'ok') { setActionError(t('profile.vehicles.defaultError')); return; }
    await reloadProfile();
  }

  if (state.kind === 'loading') return <p style={muted}>{t('profile.loading')}</p>;
  if (state.kind === 'error') return <p style={{ color: '#b91c1c' }}>{state.message}</p>;

  const { me, profile } = state;
  const activeVehicles = profile.vehicles.filter(v => v.isActive);

  return (
    <div style={page}>
      <h2 style={heading}>{t('profile.heading')}</h2>

      <section style={card}>
        <h3 style={cardTitle}>{t('profile.account.title')}</h3>
        <Row label={t('profile.account.roles')} value={formatRoles(me.roles)} />
        {/* Issue #477: surface known facility/location labels so the page
            stops looking as if facility data is missing. Pulled from the
            demo defaults until the snapshot carries per-user values. */}
        <Row label={t('profile.account.facility')} value={displayLocation(DEMO_FACILITY_ID) ?? t('labels.location.GL-HQ')} />
        <Row label={t('profile.account.location')} value={displayLocation(DEMO_LOCATION_ID) ?? DEMO_LOCATION_ID} />
      </section>

      <section style={card}>
        <h3 style={cardTitle}>{t('profile.eligibility.title')}</h3>
        <Row label={t('profile.eligibility.profileStatus')} value={profile.profileStatus} />
        <Row label={t('profile.eligibility.spotEligible')} value={profile.parkingEligible ? t('profile.yes') : t('profile.no')} />
        <Row label={t('profile.eligibility.companyCar')} value={profile.hasCompanyCar ? t('profile.yes') : t('profile.no')} />
        <Row label={t('profile.eligibility.accessibilityEligible')} value={profile.accessibilityEligible ? t('profile.yes') : t('profile.no')} />
        <Row label={t('profile.eligibility.reservedSpaceEligible')} value={profile.reservedSpaceEligible ? t('profile.yes') : t('profile.no')} />
        <Row label={t('profile.eligibility.snapshotVersion')} value={profile.snapshotVersion} />
      </section>

      <section style={card}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
          <h3 style={{ ...cardTitle, margin: 0 }}>{t('profile.vehicles.title')}</h3>
          <button
            onClick={() => { setAddOpen(o => !o); setAddError(''); }}
            style={secondaryBtn}
          >
            {addOpen ? t('profile.vehicles.cancel') : t('profile.vehicles.addToggle')}
          </button>
        </div>

        {addOpen && (
          <form onSubmit={handleAdd} style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: '12px 0', borderBottom: '1px solid #e5e7eb' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={fieldLabel}>{t('profile.vehicles.licensePlateLabel')}</label>
              <input
                value={addForm.licensePlate}
                onChange={e => setAddForm(f => ({ ...f, licensePlate: e.target.value }))}
                placeholder={t('profile.vehicles.licensePlatePlaceholder')}
                style={inputStyle}
              />
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={fieldLabel}>{t('profile.vehicles.vehicleTypeLabel')}</label>
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
                  >{tDynamic('profile.vehicleType', vt, vt)}</button>
                ))}
              </div>
            </div>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer' }}>
              <input type="checkbox" checked={addForm.isElectric} onChange={e => setAddForm(f => ({ ...f, isElectric: e.target.checked }))} />
              {t('profile.vehicles.electricCheckbox')}
            </label>
            {addError && <p style={{ margin: 0, fontSize: 13, color: '#b91c1c' }}>{addError}</p>}
            <button type="submit" disabled={busy} style={{ ...primaryBtn, opacity: busy ? 0.6 : 1 }}>
              {busy ? t('profile.vehicles.adding') : t('profile.vehicles.addSubmit')}
            </button>
          </form>
        )}

        {actionError && <p style={{ margin: '4px 0 0', fontSize: 13, color: '#b91c1c' }}>{actionError}</p>}

        {activeVehicles.length === 0 && !addOpen && (
          <p style={muted}>{t('profile.vehicles.none')}</p>
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
            {t('profile.vehicles.default')}
          </span>
        )}
      </div>
      <div style={{ ...muted, marginBottom: 8 }}>{vehicle.vehicleType} · {vehicle.isElectric ? t('profile.vehicles.electric') : t('profile.vehicles.standard')}</div>
      <div style={{ display: 'flex', gap: 8 }}>
        {!vehicle.isDefault && (
          <button onClick={onSetDefault} disabled={busy} style={ghostBtn}>{t('profile.vehicles.setDefault')}</button>
        )}
        <button onClick={onRemove} disabled={busy} style={{ ...ghostBtn, color: '#b91c1c', borderColor: '#fecaca' }}>{t('profile.vehicles.remove')}</button>
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
