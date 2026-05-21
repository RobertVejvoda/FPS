import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchReportingDashboard, fetchReportingSummary, fetchReportingFairness, downloadCsvReport,
  type DashboardResponse, type SummaryResponse, type FairnessResponse,
} from '../api/reporting';

type DashState = { kind: 'loading' } | { kind: 'ok'; data: DashboardResponse } | { kind: 'forbidden' } | { kind: 'error'; message: string };
type SumState = { kind: 'loading' } | { kind: 'ok'; data: SummaryResponse } | { kind: 'skip' } | { kind: 'error'; message: string };
type FairState = { kind: 'loading' } | { kind: 'ok'; data: FairnessResponse } | { kind: 'skip' } | { kind: 'error'; message: string };

export function ReportingPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [dash, setDash] = useState<DashState>({ kind: 'loading' });
  const [sum, setSum] = useState<SumState>({ kind: 'loading' });
  const [fair, setFair] = useState<FairState>({ kind: 'loading' });
  const [csvBusy, setCsvBusy] = useState(false);

  const load = useCallback(() => {
    setDash({ kind: 'loading' });
    setSum({ kind: 'loading' });
    setFair({ kind: 'loading' });
    const cfg = { apiBaseUrl, bearerToken };

    fetchReportingDashboard(cfg).then((r) => {
      if (r.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (r.kind === 'error' && r.status === 403) { setDash({ kind: 'forbidden' }); return; }
      if (r.kind === 'ok') setDash({ kind: 'ok', data: r.data });
      else setDash({ kind: 'error', message: 'message' in r ? r.message : 'Failed to load dashboard.' });
    });

    fetchReportingSummary(cfg).then((r) => {
      if (r.kind === 'unauthenticated') return;
      if (r.kind === 'error' && r.status === 403) { setSum({ kind: 'skip' }); return; }
      if (r.kind === 'ok') setSum({ kind: 'ok', data: r.data });
      else setSum({ kind: 'error', message: 'message' in r ? r.message : 'Failed to load summary.' });
    });

    fetchReportingFairness(cfg).then((r) => {
      if (r.kind === 'unauthenticated') return;
      if (r.kind === 'error' && r.status === 403) { setFair({ kind: 'skip' }); return; }
      if (r.kind === 'ok') setFair({ kind: 'ok', data: r.data });
      else setFair({ kind: 'error', message: 'message' in r ? r.message : 'Failed to load fairness.' });
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

  if (dash.kind === 'loading') return <p style={muted}>Loading report…</p>;
  if (dash.kind === 'forbidden') return <p style={{ color: '#b91c1c' }}>You do not have permission to view reporting data.</p>;
  if (dash.kind === 'error') return (
    <div>
      <p style={{ color: '#b91c1c' }}>{dash.message}</p>
      <button onClick={load} style={btn}>Retry</button>
    </div>
  );

  const d = dash.data;
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
            <table style={tbl}>
              <thead>
                <tr>
                  {['Date', 'Demand', 'Allocations', 'Rate'].map(h => (
                    <th key={h} style={th}>{h}</th>
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

      {sum.kind === 'ok' && sum.data.items.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>Daily Summary</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead>
                <tr>
                  {['Date', 'Location', 'Slot', 'Demand', 'Alloc', 'Rate', 'Rejected', 'Cancelled', 'No-shows'].map(h => (
                    <th key={h} style={th}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {sum.data.items.map((row, i) => (
                  <tr key={i}>
                    <td style={td}>{row.date}</td>
                    <td style={td}>{row.locationId}</td>
                    <td style={td}>{row.timeSlot}</td>
                    <td style={td}>{row.demandCount}</td>
                    <td style={td}>{row.allocationCount}</td>
                    <td style={td}>{(row.allocationRate * 100).toFixed(1)}%</td>
                    <td style={td}>{row.rejectionCount}</td>
                    <td style={td}>{row.cancellationCount}</td>
                    <td style={td}>{row.noShowCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
      {sum.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>Summary: {sum.message}</p>}

      {fair.kind === 'ok' && fair.data.items.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>Fairness</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead>
                <tr>
                  {['Requestor', 'Requests', 'Allocations', 'Rate'].map(h => (
                    <th key={h} style={th}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {fair.data.items.map(row => (
                  <tr key={row.requestorHash}>
                    <td style={td}>{row.requestorHash}</td>
                    <td style={td}>{row.requestCount}</td>
                    <td style={td}>{row.allocationCount}</td>
                    <td style={td}>{(row.allocationRate * 100).toFixed(1)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
      {fair.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>Fairness: {fair.message}</p>}
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
const tbl: React.CSSProperties = { width: '100%', borderCollapse: 'collapse', fontSize: 13 };
const th: React.CSSProperties = { textAlign: 'left', padding: '6px 8px', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontWeight: 500 };
const td: React.CSSProperties = { padding: '6px 8px', borderBottom: '1px solid #f3f4f6' };
