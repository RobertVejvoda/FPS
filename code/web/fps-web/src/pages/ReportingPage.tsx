import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchReportingDashboard, downloadCsvReport, type DashboardResponse } from '../api/reporting';

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; data: DashboardResponse }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

export function ReportingPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [csvBusy, setCsvBusy] = useState(false);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchReportingDashboard({ apiBaseUrl, bearerToken }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'error' && result.status === 403) { setState({ kind: 'forbidden' }); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', data: result.data });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load reporting data.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  async function handleCsvDownload() {
    setCsvBusy(true);
    const result = await downloadCsvReport({ apiBaseUrl, bearerToken });
    setCsvBusy(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      const url = URL.createObjectURL(result.blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'parking-summary.csv';
      a.click();
      URL.revokeObjectURL(url);
    }
  }

  if (state.kind === 'loading') return <p style={muted}>Loading report…</p>;
  if (state.kind === 'forbidden') return <p style={{ color: '#b91c1c' }}>You do not have permission to view reporting data.</p>;
  if (state.kind === 'error') return (
    <div>
      <p style={{ color: '#b91c1c' }}>{state.message}</p>
      <button onClick={load} style={btn}>Retry</button>
    </div>
  );

  const d = state.data;
  return (
    <div style={page}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Parking Reports</h2>
        <button onClick={handleCsvDownload} disabled={csvBusy} style={btn}>
          {csvBusy ? 'Downloading…' : 'Download CSV'}
        </button>
      </div>

      <div style={grid}>
        <StatCard label="Total demand" value={d.totalDemand} />
        <StatCard label="Allocations" value={d.totalAllocations} />
        <StatCard label="Allocation rate" value={`${(d.overallAllocationRate * 100).toFixed(1)}%`} />
        <StatCard label="Rejections" value={d.totalRejections} />
        <StatCard label="Cancellations" value={d.totalCancellations} />
        <StatCard label="No-shows" value={d.totalNoShows} />
      </div>

      {Object.keys(d.rejectionsByReason).length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>Rejections By Reason</h3>
          {Object.entries(d.rejectionsByReason).map(([reason, count]) => (
            <div key={reason} style={{ display: 'flex', justifyContent: 'space-between', padding: '3px 0' }}>
              <span style={muted}>{reason}</span>
              <span style={{ fontWeight: 600 }}>{count}</span>
            </div>
          ))}
        </section>
      )}

      {d.dailyTrend.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>Daily Trend</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr>
                  {['Date', 'Demand', 'Allocations', 'Rate'].map(h => (
                    <th key={h} style={{ textAlign: 'left', padding: '6px 8px', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontWeight: 500 }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {d.dailyTrend.map(row => (
                  <tr key={row.date}>
                    <td style={td}>{row.date}</td>
                    <td style={td}>{row.demand}</td>
                    <td style={td}>{row.allocations}</td>
                    <td style={td}>{(row.allocationRate * 100).toFixed(1)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div style={{ ...card, textAlign: 'center' }}>
      <div style={{ fontSize: 24, fontWeight: 700 }}>{value}</div>
      <div style={muted}>{label}</div>
    </div>
  );
}

const page: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 16 };
const grid: React.CSSProperties = { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 12 };
const card: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '16px 20px' };
const cardTitle: React.CSSProperties = { margin: '0 0 10px', fontSize: 15, fontWeight: 700 };
const muted: React.CSSProperties = { color: '#6b7280', fontSize: 13 };
const btn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
const td: React.CSSProperties = { padding: '6px 8px', borderBottom: '1px solid #f3f4f6' };
