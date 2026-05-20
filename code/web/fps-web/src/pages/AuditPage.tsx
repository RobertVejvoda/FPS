import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchAuditRecords, erasePiiMapping, type AuditRecord } from '../api/audit';

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; records: AuditRecord[]; totalCount: number }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

export function AuditPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [erasureUserId, setErasureUserId] = useState('');
  const [erasureBusy, setErasureBusy] = useState(false);
  const [erasureMsg, setErasureMsg] = useState<{ ok: boolean; text: string } | null>(null);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchAuditRecords({ apiBaseUrl, bearerToken }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'error' && result.status === 403) { setState({ kind: 'forbidden' }); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', records: result.data.items, totalCount: result.data.totalCount });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load audit records.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  async function handleErasure(e: React.FormEvent) {
    e.preventDefault();
    const uid = erasureUserId.trim();
    if (!uid) return;
    if (!confirm(`Permanently erase PII mapping for user ID: ${uid}?\n\nThis action cannot be undone.`)) return;
    setErasureBusy(true);
    const result = await erasePiiMapping({ apiBaseUrl, bearerToken }, uid);
    setErasureBusy(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      setErasureMsg({ ok: true, text: `PII mapping erased for user ID: ${uid}` });
      setErasureUserId('');
    } else {
      setErasureMsg({ ok: false, text: 'message' in result ? result.message : 'Erasure failed.' });
    }
    setTimeout(() => setErasureMsg(null), 6000);
  }

  return (
    <div style={page}>
      <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Audit Console</h2>

      <section style={card}>
        <h3 style={cardTitle}>GDPR PII Mapping Erasure</h3>
        <p style={muted}>Permanently removes the PII mapping for a user ID. Audit records remain in pseudonymised form.</p>
        {erasureMsg && (
          <div style={{ padding: '8px 14px', borderRadius: 6, background: erasureMsg.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${erasureMsg.ok ? '#bbf7d0' : '#fecaca'}`, color: erasureMsg.ok ? '#166534' : '#b91c1c', fontSize: 13, marginBottom: 10 }}>
            {erasureMsg.text}
          </div>
        )}
        <form onSubmit={handleErasure} style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, flex: 1, minWidth: 200 }}>
            <label style={muted}>User ID</label>
            <input
              value={erasureUserId}
              onChange={e => setErasureUserId(e.target.value)}
              placeholder="e.g. a1b2c3d4-..."
              style={{ border: '1px solid #d1d5db', borderRadius: 6, padding: '7px 10px', fontSize: 14, outline: 'none' }}
            />
          </div>
          <button type="submit" disabled={erasureBusy || !erasureUserId.trim()}
            style={{ ...dangerBtn, opacity: !erasureUserId.trim() || erasureBusy ? 0.5 : 1 }}>
            {erasureBusy ? 'Erasing…' : 'Erase PII mapping'}
          </button>
        </form>
      </section>

      <section style={card}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <h3 style={{ ...cardTitle, margin: 0 }}>
            Audit Records {state.kind === 'ok' ? <span style={{ ...muted, fontWeight: 400 }}>({state.totalCount} total)</span> : null}
          </h3>
          <button onClick={load} style={btn}>Refresh</button>
        </div>

        {state.kind === 'loading' && <p style={muted}>Loading…</p>}
        {state.kind === 'forbidden' && <p style={{ color: '#b91c1c' }}>You do not have permission to query audit records.</p>}
        {state.kind === 'error' && <p style={{ color: '#b91c1c' }}>{state.message}</p>}
        {state.kind === 'ok' && state.records.length === 0 && <p style={muted}>No audit records found.</p>}
        {state.kind === 'ok' && state.records.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr>
                  {['Occurred', 'Event type', 'Entity type', 'Entity ID', 'Actor type', 'Actor (pseudonymised)'].map(h => (
                    <th key={h} style={{ textAlign: 'left', padding: '7px 10px', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontWeight: 500, whiteSpace: 'nowrap' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {state.records.map(r => (
                  <tr key={r.auditRecordId} style={{ borderBottom: '1px solid #f3f4f6' }}>
                    <td style={td}>{new Date(r.occurredAt).toLocaleString()}</td>
                    <td style={td}>{r.eventType}</td>
                    <td style={td}>{r.entityType}</td>
                    <td style={{ ...td, color: '#6b7280' }}>{r.entityId ?? '—'}</td>
                    <td style={td}>{r.actorType}</td>
                    <td style={{ ...td, color: '#6b7280', fontFamily: 'monospace', fontSize: 11 }}>{r.actorHash ? r.actorHash.slice(0, 12) + '…' : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

const page: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 16 };
const card: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '16px 20px' };
const cardTitle: React.CSSProperties = { margin: '0 0 8px', fontSize: 15, fontWeight: 700 };
const muted: React.CSSProperties = { color: '#6b7280', fontSize: 13 };
const btn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, padding: '7px 14px', fontSize: 13, fontWeight: 500, cursor: 'pointer' };
const dangerBtn: React.CSSProperties = { background: '#b91c1c', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
const td: React.CSSProperties = { padding: '7px 10px' };
