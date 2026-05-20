import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchParkingPolicy, saveParkingPolicy, type ParkingPolicy } from '../api/configuration';

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; policy: ParkingPolicy; dirty: Partial<ParkingPolicy>; saved: boolean }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

export function ConfigurationPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [saving, setSaving] = useState(false);
  const [saveMsg, setSaveMsg] = useState<{ ok: boolean; text: string } | null>(null);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchParkingPolicy({ apiBaseUrl, bearerToken }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'error' && result.status === 403) { setState({ kind: 'forbidden' }); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', policy: result.data, dirty: {}, saved: false });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load policy.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  function patch(field: keyof ParkingPolicy, value: unknown) {
    setState(prev => prev.kind === 'ok'
      ? { ...prev, dirty: { ...prev.dirty, [field]: value }, saved: false }
      : prev);
  }

  async function handleSave() {
    if (state.kind !== 'ok') return;
    const merged = { ...state.policy, ...state.dirty };
    setSaving(true);
    const result = await saveParkingPolicy({ apiBaseUrl, bearerToken }, merged);
    setSaving(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      setSaveMsg({ ok: true, text: 'Policy saved.' });
      setState(prev => prev.kind === 'ok' ? { ...prev, policy: merged, dirty: {}, saved: true } : prev);
    } else {
      setSaveMsg({ ok: false, text: 'message' in result ? result.message : 'Failed to save policy.' });
    }
    setTimeout(() => setSaveMsg(null), 4000);
  }

  if (state.kind === 'loading') return <p style={muted}>Loading policy…</p>;
  if (state.kind === 'forbidden') return <p style={{ color: '#b91c1c' }}>You do not have permission to view or edit configuration.</p>;
  if (state.kind === 'error') return (
    <div>
      <p style={{ color: '#b91c1c' }}>{state.message}</p>
      <button onClick={load} style={btn}>Retry</button>
    </div>
  );

  const current = { ...state.policy, ...state.dirty };
  const hasDirty = Object.keys(state.dirty).length > 0;

  return (
    <div style={page}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Configuration</h2>
        <button onClick={handleSave} disabled={saving || !hasDirty} style={{ ...btn, opacity: !hasDirty || saving ? 0.5 : 1 }}>
          {saving ? 'Saving…' : 'Save policy'}
        </button>
      </div>

      {saveMsg && (
        <div style={{ padding: '10px 16px', borderRadius: 8, background: saveMsg.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${saveMsg.ok ? '#bbf7d0' : '#fecaca'}`, color: saveMsg.ok ? '#166534' : '#b91c1c', fontSize: 13, fontWeight: 500 }}>
          {saveMsg.text}
        </div>
      )}

      <section style={card}>
        <h3 style={cardTitle}>Parking Policy</h3>
        <div style={fieldGrid}>
          <Field label="Time zone" value={current.timeZone} onChange={v => patch('timeZone', v)} />
          <Field label="Draw cut-off time" value={current.drawCutOffTime} onChange={v => patch('drawCutOffTime', v)} />
          <NumField label="Daily request cap" value={current.dailyRequestCap} onChange={v => patch('dailyRequestCap', v)} />
          <NumField label="Allocation lookback days" value={current.allocationLookbackDays} onChange={v => patch('allocationLookbackDays', v)} />
          <NumField label="Late cancellation penalty" value={current.lateCancellationPenalty} onChange={v => patch('lateCancellationPenalty', v)} />
          <NumField label="No-show penalty" value={current.noShowPenalty} onChange={v => patch('noShowPenalty', v)} />
          <NumField label="Usage confirmation window (min)" value={current.usageConfirmationWindowMinutes} onChange={v => patch('usageConfirmationWindowMinutes', v)} />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 12 }}>
          <CheckField label="Manual adjustment enabled" checked={current.manualAdjustmentEnabled} onChange={v => patch('manualAdjustmentEnabled', v)} />
          <CheckField label="Same-day booking enabled" checked={current.sameDayBookingEnabled} onChange={v => patch('sameDayBookingEnabled', v)} />
          <CheckField label="Same-day uses request cap" checked={current.sameDayUsesRequestCap} onChange={v => patch('sameDayUsesRequestCap', v)} />
          <CheckField label="Automatic reallocation" checked={current.automaticReallocationEnabled} onChange={v => patch('automaticReallocationEnabled', v)} />
          <CheckField label="Usage confirmation required" checked={current.usageConfirmationRequired} onChange={v => patch('usageConfirmationRequired', v)} />
          <CheckField label="No-show detection enabled" checked={current.noShowDetectionEnabled} onChange={v => patch('noShowDetectionEnabled', v)} />
          <CheckField label="Company car tier 1 enabled" checked={current.companyCarTier1Enabled} onChange={v => patch('companyCarTier1Enabled', v)} />
        </div>
        <div style={{ marginTop: 10 }}>
          <span style={muted}>Version: {state.policy.version}</span>
        </div>
      </section>
    </div>
  );
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <label style={muted}>{label}</label>
      <input value={value} onChange={e => onChange(e.target.value)}
        style={{ border: '1px solid #d1d5db', borderRadius: 6, padding: '7px 10px', fontSize: 14, outline: 'none' }} />
    </div>
  );
}

function NumField({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <label style={muted}>{label}</label>
      <input type="number" value={value} onChange={e => onChange(Number(e.target.value))}
        style={{ border: '1px solid #d1d5db', borderRadius: 6, padding: '7px 10px', fontSize: 14, outline: 'none' }} />
    </div>
  );
}

function CheckField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer' }}>
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />
      <span style={{ fontSize: 14 }}>{label}</span>
    </label>
  );
}

const page: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 16 };
const card: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '16px 20px' };
const cardTitle: React.CSSProperties = { margin: '0 0 12px', fontSize: 15, fontWeight: 700 };
const fieldGrid: React.CSSProperties = { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12 };
const muted: React.CSSProperties = { color: '#6b7280', fontSize: 13 };
const btn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
