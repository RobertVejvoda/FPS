import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchParkingPolicy, saveParkingPolicy,
  fetchLocationPolicy, saveLocationPolicy,
  fetchPolicyHistory, fetchSlots, fetchSlotHistory,
  type ParkingPolicy, type PolicyHistoryItem, type SlotDto, type SlotHistoryItem,
} from '../api/configuration';

type TenantState =
  | { kind: 'loading' }
  | { kind: 'ok'; policy: ParkingPolicy; dirty: Partial<ParkingPolicy>; saved: boolean }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

type LocState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ok'; policy: ParkingPolicy; dirty: Partial<ParkingPolicy> }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

type HistoryState = { kind: 'idle' } | { kind: 'loading' } | { kind: 'ok'; items: PolicyHistoryItem[] } | { kind: 'error'; message: string };
type SlotsState = { kind: 'idle' } | { kind: 'loading' } | { kind: 'ok'; slots: SlotDto[]; history: SlotHistoryItem[] } | { kind: 'error'; message: string };

export function ConfigurationPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const cfg = { apiBaseUrl, bearerToken };

  const [tenant, setTenant] = useState<TenantState>({ kind: 'loading' });
  const [saving, setSaving] = useState(false);
  const [saveMsg, setSaveMsg] = useState<{ ok: boolean; text: string } | null>(null);

  const [locationId, setLocationId] = useState('');
  const [locState, setLocState] = useState<LocState>({ kind: 'idle' });
  const [locSaving, setLocSaving] = useState(false);
  const [locSaveMsg, setLocSaveMsg] = useState<{ ok: boolean; text: string } | null>(null);

  const [history, setHistory] = useState<HistoryState>({ kind: 'idle' });
  const [slots, setSlots] = useState<SlotsState>({ kind: 'idle' });

  const loadTenant = useCallback(() => {
    setTenant({ kind: 'loading' });
    fetchParkingPolicy(cfg).then((r) => {
      if (r.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (r.kind === 'error' && r.status === 403) { setTenant({ kind: 'forbidden' }); return; }
      if (r.kind === 'ok') setTenant({ kind: 'ok', policy: r.data, dirty: {}, saved: false });
      else setTenant({ kind: 'error', message: 'message' in r ? r.message : 'Failed to load policy.' });
    });
  }, [apiBaseUrl, bearerToken]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => { loadTenant(); }, [loadTenant]);

  function patchTenant(field: keyof ParkingPolicy, value: unknown) {
    setTenant(prev => prev.kind === 'ok'
      ? { ...prev, dirty: { ...prev.dirty, [field]: value }, saved: false }
      : prev);
  }

  async function handleSaveTenant() {
    if (tenant.kind !== 'ok') return;
    const merged = { ...tenant.policy, ...tenant.dirty };
    setSaving(true);
    const r = await saveParkingPolicy(cfg, merged);
    setSaving(false);
    if (r.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (r.kind === 'ok') {
      setSaveMsg({ ok: true, text: 'Policy saved.' });
      setTenant(prev => prev.kind === 'ok' ? { ...prev, policy: merged, dirty: {}, saved: true } : prev);
    } else {
      setSaveMsg({ ok: false, text: 'message' in r ? r.message : 'Failed to save policy.' });
    }
    setTimeout(() => setSaveMsg(null), 4000);
  }

  function loadLocation() {
    const id = locationId.trim();
    if (!id) return;
    setLocState({ kind: 'loading' });
    setSlots({ kind: 'loading' });
    fetchLocationPolicy(cfg, id).then((r) => {
      if (r.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (r.kind === 'error' && r.status === 403) { setLocState({ kind: 'forbidden' }); return; }
      if (r.kind === 'ok') setLocState({ kind: 'ok', policy: r.data, dirty: {} });
      else setLocState({ kind: 'error', message: 'message' in r ? r.message : 'No location policy.' });
    });
    Promise.all([fetchSlots(cfg, id), fetchSlotHistory(cfg, id)]).then(([sr, hr]) => {
      if (sr.kind === 'ok' && hr.kind === 'ok') {
        setSlots({ kind: 'ok', slots: sr.data, history: hr.data });
      } else {
        const msg = sr.kind !== 'ok' && 'message' in sr ? sr.message : hr.kind !== 'ok' && 'message' in hr ? hr.message : 'Failed to load slots.';
        setSlots({ kind: 'error', message: msg });
      }
    });
  }

  function patchLoc(field: keyof ParkingPolicy, value: unknown) {
    setLocState(prev => prev.kind === 'ok'
      ? { ...prev, dirty: { ...prev.dirty, [field]: value } }
      : prev);
  }

  async function handleSaveLocation() {
    if (locState.kind !== 'ok') return;
    const id = locationId.trim();
    const merged = { ...locState.policy, ...locState.dirty };
    setLocSaving(true);
    const r = await saveLocationPolicy(cfg, id, merged);
    setLocSaving(false);
    if (r.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (r.kind === 'ok') {
      setLocSaveMsg({ ok: true, text: 'Location policy saved.' });
      setLocState(prev => prev.kind === 'ok' ? { ...prev, policy: merged, dirty: {} } : prev);
    } else {
      setLocSaveMsg({ ok: false, text: 'message' in r ? r.message : 'Failed to save location policy.' });
    }
    setTimeout(() => setLocSaveMsg(null), 4000);
  }

  function loadHistory() {
    setHistory({ kind: 'loading' });
    fetchPolicyHistory(cfg).then((r) => {
      if (r.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (r.kind === 'ok') setHistory({ kind: 'ok', items: r.data });
      else setHistory({ kind: 'error', message: 'message' in r ? r.message : 'Failed to load history.' });
    });
  }

  if (tenant.kind === 'loading') return <p style={muted}>Loading policy…</p>;
  if (tenant.kind === 'forbidden') return <p style={{ color: '#b91c1c' }}>You do not have permission to view or edit configuration.</p>;
  if (tenant.kind === 'error') return (
    <div>
      <p style={{ color: '#b91c1c' }}>{tenant.message}</p>
      <button onClick={loadTenant} style={btn}>Retry</button>
    </div>
  );

  const current = { ...tenant.policy, ...tenant.dirty };
  const hasDirty = Object.keys(tenant.dirty).length > 0;

  return (
    <div style={page}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Configuration</h2>
        <button onClick={handleSaveTenant} disabled={saving || !hasDirty} style={{ ...btn, opacity: !hasDirty || saving ? 0.5 : 1 }}>
          {saving ? 'Saving…' : 'Save policy'}
        </button>
      </div>

      {saveMsg && <SaveBanner ok={saveMsg.ok} text={saveMsg.text} />}

      <section style={card}>
        <h3 style={cardTitle}>Tenant Parking Policy</h3>
        <PolicyForm policy={current} onPatch={patchTenant} />
        <div style={{ marginTop: 10 }}>
          <span style={muted}>Version: {tenant.policy.version}</span>
        </div>
      </section>

      <section style={card}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <h3 style={{ ...cardTitle, marginBottom: 0 }}>Policy Version History</h3>
          {history.kind === 'idle' && <button onClick={loadHistory} style={btnSm}>Load history</button>}
          {history.kind === 'loading' && <span style={muted}>Loading…</span>}
        </div>
        {history.kind === 'ok' && (
          history.items.length === 0 ? <p style={muted}>No history.</p> : (
            <div style={{ overflowX: 'auto' }}>
              <table style={tbl}>
                <thead>
                  <tr>
                    {['Version', 'Published at', 'Published by', 'Reason'].map(h => (
                      <th key={h} style={th}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {history.items.map(item => (
                    <tr key={item.version}>
                      <td style={td}>{item.version}</td>
                      <td style={td}>{item.publishedAt}</td>
                      <td style={td}>{item.publishedByHash ?? '—'}</td>
                      <td style={td}>{item.publicationReason ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        )}
        {history.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>{history.message}</p>}
      </section>

      <section style={card}>
        <h3 style={cardTitle}>Location Configuration</h3>
        <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
          <input
            value={locationId}
            onChange={e => setLocationId(e.target.value)}
            placeholder="Location ID"
            style={{ flex: 1, border: '1px solid #d1d5db', borderRadius: 6, padding: '7px 10px', fontSize: 14, outline: 'none' }}
          />
          <button onClick={loadLocation} disabled={!locationId.trim()} style={{ ...btn, opacity: !locationId.trim() ? 0.5 : 1 }}>
            Load
          </button>
        </div>

        {locState.kind === 'loading' && <p style={muted}>Loading location policy…</p>}
        {locState.kind === 'forbidden' && <p style={{ color: '#b91c1c', fontSize: 13 }}>Insufficient permissions for this location.</p>}
        {locState.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>{locState.message}</p>}

        {locState.kind === 'ok' && (
          <div>
            {locSaveMsg && <SaveBanner ok={locSaveMsg.ok} text={locSaveMsg.text} />}
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
              <span style={muted}>Location: {locationId.trim()} · Version: {locState.policy.version}</span>
              <button
                onClick={handleSaveLocation}
                disabled={locSaving || Object.keys(locState.dirty).length === 0}
                style={{ ...btnSm, opacity: Object.keys(locState.dirty).length === 0 || locSaving ? 0.5 : 1 }}
              >
                {locSaving ? 'Saving…' : 'Save location policy'}
              </button>
            </div>
            <PolicyForm policy={{ ...locState.policy, ...locState.dirty }} onPatch={patchLoc} />
          </div>
        )}

        {slots.kind === 'loading' && <p style={{ ...muted, marginTop: 16 }}>Loading slots…</p>}
        {slots.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13, marginTop: 16 }}>{slots.message}</p>}
        {slots.kind === 'ok' && (
          <div style={{ marginTop: 20 }}>
            <h4 style={{ margin: '0 0 8px', fontSize: 14, fontWeight: 600 }}>Slots ({slots.slots.length})</h4>
            {slots.slots.length === 0 ? <p style={muted}>No slots configured.</p> : (
              <div style={{ overflowX: 'auto' }}>
                <table style={tbl}>
                  <thead>
                    <tr>
                      {['Slot ID', 'Active', 'Charger', 'Accessible', 'Company car', 'Moto', 'Reserved for'].map(h => (
                        <th key={h} style={th}>{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {slots.slots.map(s => (
                      <tr key={s.slotId}>
                        <td style={td}>{s.slotId}</td>
                        <td style={td}>{s.isActive ? 'Yes' : 'No'}</td>
                        <td style={td}>{s.hasCharger ? 'Yes' : 'No'}</td>
                        <td style={td}>{s.isAccessible ? 'Yes' : 'No'}</td>
                        <td style={td}>{s.isCompanyCarOnly ? 'Yes' : 'No'}</td>
                        <td style={td}>{s.isMotorcycleCapacity ? 'Yes' : 'No'}</td>
                        <td style={td}>{s.reservedForUserId ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {slots.history.length > 0 && (
              <div style={{ marginTop: 16 }}>
                <h4 style={{ margin: '0 0 8px', fontSize: 14, fontWeight: 600 }}>Slot History</h4>
                <div style={{ overflowX: 'auto' }}>
                  <table style={tbl}>
                    <thead>
                      <tr>
                        {['Version', 'Changed at', 'Changed by', 'Reason', 'Count'].map(h => (
                          <th key={h} style={th}>{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {slots.history.map(item => (
                        <tr key={item.version}>
                          <td style={td}>{item.version}</td>
                          <td style={td}>{item.changedAt}</td>
                          <td style={td}>{item.changedByHash ?? '—'}</td>
                          <td style={td}>{item.changeReason ?? '—'}</td>
                          <td style={td}>{item.slotCount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        )}
      </section>
    </div>
  );
}

function PolicyForm({ policy, onPatch }: { policy: ParkingPolicy; onPatch: (field: keyof ParkingPolicy, value: unknown) => void }) {
  return (
    <div>
      <div style={fieldGrid}>
        <Field label="Time zone" value={policy.timeZone} onChange={v => onPatch('timeZone', v)} />
        <Field label="Draw cut-off time" value={policy.drawCutOffTime} onChange={v => onPatch('drawCutOffTime', v)} />
        <NumField label="Daily request cap" value={policy.dailyRequestCap} onChange={v => onPatch('dailyRequestCap', v)} />
        <NumField label="Allocation lookback days" value={policy.allocationLookbackDays} onChange={v => onPatch('allocationLookbackDays', v)} />
        <NumField label="Late cancellation penalty" value={policy.lateCancellationPenalty} onChange={v => onPatch('lateCancellationPenalty', v)} />
        <NumField label="No-show penalty" value={policy.noShowPenalty} onChange={v => onPatch('noShowPenalty', v)} />
        <NumField label="Usage confirmation window (min)" value={policy.usageConfirmationWindowMinutes} onChange={v => onPatch('usageConfirmationWindowMinutes', v)} />
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 12 }}>
        <CheckField label="Manual adjustment enabled" checked={policy.manualAdjustmentEnabled} onChange={v => onPatch('manualAdjustmentEnabled', v)} />
        <CheckField label="Same-day booking enabled" checked={policy.sameDayBookingEnabled} onChange={v => onPatch('sameDayBookingEnabled', v)} />
        <CheckField label="Same-day uses request cap" checked={policy.sameDayUsesRequestCap} onChange={v => onPatch('sameDayUsesRequestCap', v)} />
        <CheckField label="Automatic reallocation" checked={policy.automaticReallocationEnabled} onChange={v => onPatch('automaticReallocationEnabled', v)} />
        <CheckField label="Usage confirmation required" checked={policy.usageConfirmationRequired} onChange={v => onPatch('usageConfirmationRequired', v)} />
        <CheckField label="No-show detection enabled" checked={policy.noShowDetectionEnabled} onChange={v => onPatch('noShowDetectionEnabled', v)} />
        <CheckField label="Company car tier 1 enabled" checked={policy.companyCarTier1Enabled} onChange={v => onPatch('companyCarTier1Enabled', v)} />
      </div>
    </div>
  );
}

function SaveBanner({ ok, text }: { ok: boolean; text: string }) {
  return (
    <div style={{ padding: '10px 16px', borderRadius: 8, background: ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${ok ? '#bbf7d0' : '#fecaca'}`, color: ok ? '#166534' : '#b91c1c', fontSize: 13, fontWeight: 500 }}>
      {text}
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
const btnSm: React.CSSProperties = { ...btn, padding: '6px 12px', fontSize: 13 };
const tbl: React.CSSProperties = { width: '100%', borderCollapse: 'collapse', fontSize: 13 };
const th: React.CSSProperties = { textAlign: 'left', padding: '6px 8px', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontWeight: 500 };
const td: React.CSSProperties = { padding: '6px 8px', borderBottom: '1px solid #f3f4f6' };
