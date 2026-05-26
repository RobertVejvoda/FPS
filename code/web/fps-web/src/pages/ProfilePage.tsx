import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchProfileSnapshot, type ProfileSnapshot } from '../api/profile';
import { fetchMe, type MeResponse } from '../api/client';

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; me: MeResponse; profile: ProfileSnapshot }
  | { kind: 'error'; message: string };

export function ProfilePage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'loading' });

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

  if (state.kind === 'loading') return <p style={muted}>Loading profile…</p>;
  if (state.kind === 'error') return <p style={{ color: '#b91c1c' }}>{state.message}</p>;

  const { me, profile } = state;
  return (
    <div style={page}>
      <h2 style={heading}>My Profile</h2>

      <section style={card}>
        <h3 style={cardTitle}>Account</h3>
        <Row label="Roles" value={me.roles.length ? me.roles.join(', ') : 'Employee'} />
      </section>

      <section style={card}>
        <h3 style={cardTitle}>Spot Eligibility</h3>
        <Row label="Profile status" value={profile.profileStatus} />
        <Row label="Parking eligible" value={profile.parkingEligible ? 'Yes' : 'No'} />
        <Row label="Company car" value={profile.hasCompanyCar ? 'Yes' : 'No'} />
        <Row label="Accessibility eligible" value={profile.accessibilityEligible ? 'Yes' : 'No'} />
        <Row label="Reserved space eligible" value={profile.reservedSpaceEligible ? 'Yes' : 'No'} />
        <Row label="Snapshot version" value={profile.snapshotVersion} />
      </section>

      <section style={card}>
        <h3 style={cardTitle}>Active Vehicles</h3>
        {profile.vehicles.filter(v => v.isActive).length === 0 ? (
          <p style={muted}>No active vehicles linked to this profile.</p>
        ) : (
          profile.vehicles.filter(v => v.isActive).map(v => (
            <div key={v.vehicleId} style={{ borderTop: '1px solid #e5e7eb', paddingTop: 10, marginTop: 10 }}>
              <div style={{ fontWeight: 600 }}>{v.licensePlate || v.vehicleType}</div>
              <div style={muted}>{v.vehicleType} · {v.isElectric ? 'Electric' : 'Standard'}</div>
            </div>
          ))
        )}
      </section>
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
