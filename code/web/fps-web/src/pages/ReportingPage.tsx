import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchReportingDashboard, fetchReportingSummary, fetchReportingFairness,
  fetchUtilizationReport, fetchReasonCodeReport, fetchEmployeeImpact, fetchOperationalExceptions,
  downloadCsvReport, downloadAllocationOutcomesCsv,
  type DashboardResponse, type SummaryResponse, type FairnessResponse,
  type UtilizationResponse, type ReasonCodeResponse, type EmployeeImpactResponse,
  type OperationalExceptionsResponse,
} from '../api/reporting';
import { displayLocation, displayRequestorRef, shortRequestorRef } from '../displayLabels';
import { fetchHrDisplayNames } from '../api/profile';
import { t, formatDate } from '../i18n';

type DashState = { kind: 'loading' } | { kind: 'ok'; data: DashboardResponse } | { kind: 'forbidden' } | { kind: 'error'; message: string };
type SumState = { kind: 'loading' } | { kind: 'ok'; data: SummaryResponse } | { kind: 'skip' } | { kind: 'error'; message: string };
type FairState = { kind: 'loading' } | { kind: 'ok'; data: FairnessResponse } | { kind: 'skip' } | { kind: 'error'; message: string };
type UtilState = { kind: 'loading' } | { kind: 'ok'; data: UtilizationResponse } | { kind: 'skip' } | { kind: 'error'; message: string };
type RcState = { kind: 'loading' } | { kind: 'ok'; data: ReasonCodeResponse } | { kind: 'skip' } | { kind: 'error'; message: string };
type EmpImpactState = { kind: 'loading' } | { kind: 'ok'; data: EmployeeImpactResponse } | { kind: 'skip' } | { kind: 'error'; message: string };
type OpsState = { kind: 'loading' } | { kind: 'ok'; data: OperationalExceptionsResponse } | { kind: 'skip' } | { kind: 'error'; message: string };

export function ReportingPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [dash, setDash] = useState<DashState>({ kind: 'loading' });
  const [sum, setSum] = useState<SumState>({ kind: 'loading' });
  const [fair, setFair] = useState<FairState>({ kind: 'loading' });
  const [util, setUtil] = useState<UtilState>({ kind: 'loading' });
  const [rc, setRc] = useState<RcState>({ kind: 'loading' });
  const [empImpact, setEmpImpact] = useState<EmpImpactState>({ kind: 'loading' });
  const [ops, setOps] = useState<OpsState>({ kind: 'loading' });
  const [csvBusy, setCsvBusy] = useState(false);
  const [outcomesBusy, setOutcomesBusy] = useState(false);
  // Display-name lookup for Fairness + Employee Impact rows (issue #474).
  // Single fetch keyed off the union of refs in both tables; the rows then
  // render `displayName ?? displayRequestorRef(ref)`, so HR sees employee
  // names instead of `Requestor <hash-prefix>` — and the short ref remains
  // available as a fallback when the lookup misses.
  const [displayNames, setDisplayNames] = useState<Record<string, string | null>>({});
  // True after the display-name lookup has completed (or failed) for the
  // current refs. Until then the rows render the regular short ref so the
  // brief in-flight moment doesn't flash "Unknown requestor" at the user.
  const [displayNamesLoaded, setDisplayNamesLoaded] = useState(false);

  const load = useCallback(() => {
    setDash({ kind: 'loading' });
    setSum({ kind: 'loading' });
    setFair({ kind: 'loading' });
    setUtil({ kind: 'loading' });
    setRc({ kind: 'loading' });
    setEmpImpact({ kind: 'loading' });
    setOps({ kind: 'loading' });
    const cfg = { apiBaseUrl, bearerToken };

    fetchReportingDashboard(cfg).then((r) => {
      if (r.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (r.kind === 'error' && r.status === 403) { setDash({ kind: 'forbidden' }); return; }
      if (r.kind === 'ok') setDash({ kind: 'ok', data: r.data });
      else setDash({ kind: 'error', message: 'message' in r ? r.message : t('reporting.error.dashboard') });
    });

    fetchReportingSummary(cfg).then((r) => {
      if (r.kind === 'unauthenticated') return;
      if (r.kind === 'error' && r.status === 403) { setSum({ kind: 'skip' }); return; }
      if (r.kind === 'ok') setSum({ kind: 'ok', data: r.data });
      else setSum({ kind: 'error', message: 'message' in r ? r.message : t('reporting.error.summary') });
    });

    fetchReportingFairness(cfg).then((r) => {
      if (r.kind === 'unauthenticated') return;
      if (r.kind === 'error' && r.status === 403) { setFair({ kind: 'skip' }); return; }
      if (r.kind === 'ok') setFair({ kind: 'ok', data: r.data });
      else setFair({ kind: 'error', message: 'message' in r ? r.message : t('reporting.error.fairness') });
    });

    fetchUtilizationReport(cfg).then((r) => {
      if (r.kind === 'unauthenticated') return;
      if (r.kind === 'error' && r.status === 403) { setUtil({ kind: 'skip' }); return; }
      if (r.kind === 'ok') setUtil({ kind: 'ok', data: r.data });
      else setUtil({ kind: 'error', message: 'message' in r ? r.message : t('reporting.error.utilization') });
    });

    fetchReasonCodeReport(cfg).then((r) => {
      if (r.kind === 'unauthenticated') return;
      if (r.kind === 'error' && r.status === 403) { setRc({ kind: 'skip' }); return; }
      if (r.kind === 'ok') setRc({ kind: 'ok', data: r.data });
      else setRc({ kind: 'error', message: 'message' in r ? r.message : t('reporting.error.reasonCodes') });
    });

    fetchEmployeeImpact(cfg, 2).then((r) => {
      if (r.kind === 'unauthenticated') return;
      if (r.kind === 'error' && r.status === 403) { setEmpImpact({ kind: 'skip' }); return; }
      if (r.kind === 'ok') setEmpImpact({ kind: 'ok', data: r.data });
      else setEmpImpact({ kind: 'error', message: 'message' in r ? r.message : t('reporting.error.employeeImpact') });
    });

    fetchOperationalExceptions(cfg).then((r) => {
      if (r.kind === 'unauthenticated') return;
      if (r.kind === 'error' && r.status === 403) { setOps({ kind: 'skip' }); return; }
      if (r.kind === 'ok') setOps({ kind: 'ok', data: r.data });
      else setOps({ kind: 'error', message: 'message' in r ? r.message : t('reporting.error.operationalExceptions') });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  // Re-run the display-name lookup whenever Fairness or Employee Impact data
  // changes. The lookup endpoint is allowed for hr_manager, admin AND
  // report_viewer (issue #474 — relaxed in the same PR so the Reports
  // surface always resolves names, not just for HR). If the lookup ever
  // does fail — e.g. a future tenant carves a narrower role — rowLabel
  // commits to the explicit "Unknown requestor · <short ref>" fallback
  // (issue #480).
  useEffect(() => {
    const refs = new Set<string>();
    if (fair.kind === 'ok') for (const row of fair.data.items) if (row.requestorRef) refs.add(row.requestorRef);
    if (empImpact.kind === 'ok') for (const row of empImpact.data.items) if (row.requestorRef) refs.add(row.requestorRef);
    if (refs.size === 0) return;
    setDisplayNamesLoaded(false);
    let cancelled = false;
    void fetchHrDisplayNames({ apiBaseUrl, bearerToken }, [...refs]).then(r => {
      if (cancelled) return;
      // Flip the loaded flag regardless of result so the page commits to the
      // explicit "Unknown requestor · short-ref" fallback even when the lookup
      // fails (e.g. transient 5xx) — instead of leaving rows on the more
      // ambiguous bare short-ref render.
      if (r.kind === 'ok') setDisplayNames(prev => ({ ...prev, ...r.data.names }));
      setDisplayNamesLoaded(true);
    });
    return () => { cancelled = true; };
  }, [fair, empImpact, apiBaseUrl, bearerToken]);

  // Resolve a row's display label per #480 acceptance criteria:
  //   1. employee display name if Profile knows it,
  //   2. explicit "Unknown requestor · <short ref>" once the lookup completed
  //      and returned no name for this ref,
  //   3. plain short ref while the lookup is still in flight (prevents the
  //      "Unknown" flash on first paint).
  function rowLabel(ref: string): string {
    const name = displayNames[ref];
    if (name) return name;
    if (displayNamesLoaded) return t('reporting.unknownRequestor', { ref: shortRequestorRef(ref) });
    return displayRequestorRef(ref);
  }

  async function triggerDownload(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = filename; a.click();
    URL.revokeObjectURL(url);
  }

  async function handleCsvDownload() {
    setCsvBusy(true);
    const result = await downloadCsvReport({ apiBaseUrl, bearerToken });
    setCsvBusy(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') triggerDownload(result.blob, 'parking-summary.csv');
  }

  async function handleOutcomesCsvDownload() {
    setOutcomesBusy(true);
    const result = await downloadAllocationOutcomesCsv({ apiBaseUrl, bearerToken });
    setOutcomesBusy(false);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') triggerDownload(result.blob, 'parking-allocation-outcomes.csv');
  }

  if (dash.kind === 'loading') return <p style={muted}>{t('reporting.loadingReport')}</p>;
  if (dash.kind === 'forbidden') return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <p style={{ color: '#b91c1c', margin: 0 }}>{t('reporting.forbiddenTitle')}</p>
      <p style={{ ...muted, margin: 0 }}>{t('reporting.forbiddenDetail')}</p>
    </div>
  );
  if (dash.kind === 'error') return (
    <div>
      <p style={{ color: '#b91c1c' }}>{dash.message}</p>
      <button onClick={load} style={btn}>{t('reporting.retry')}</button>
    </div>
  );

  const d = dash.data;
  const hasData = d.totalDemand > 0 || d.totalAllocations > 0 || d.totalRejections > 0;

  return (
    <div style={page}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>{t('reporting.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <button onClick={handleOutcomesCsvDownload} disabled={outcomesBusy} style={btnSm}>
            {outcomesBusy ? t('reporting.downloading') : t('reporting.outcomesCsv')}
          </button>
          <button onClick={handleCsvDownload} disabled={csvBusy} style={btn}>
            {csvBusy ? t('reporting.downloading') : t('reporting.summaryCsv')}
          </button>
        </div>
      </div>

      {!hasData && (
        <section style={{ ...card, background: '#f8fafc', border: '1px solid #e2e8f0' }}>
          <p style={{ margin: 0, fontWeight: 600, fontSize: 14, color: '#374151' }}>{t('reporting.noDataTitle')}</p>
          <p style={{ ...muted, margin: '6px 0 0' }}>
            {t('reporting.noDataDetail')}
          </p>
        </section>
      )}

      <div style={grid}>
        <StatCard label={t('reporting.stat.totalDemand')} value={d.totalDemand} />
        <StatCard label={t('reporting.stat.allocations')} value={d.totalAllocations} />
        <StatCard label={t('reporting.stat.allocationRate')} value={`${(d.overallAllocationRate * 100).toFixed(1)}%`} />
        <StatCard label={t('reporting.stat.rejections')} value={d.totalRejections} />
        <StatCard label={t('reporting.stat.cancellations')} value={d.totalCancellations} />
        <StatCard label={t('reporting.stat.noShows')} value={d.totalNoShows} />
      </div>

      {Object.keys(d.rejectionsByReason).length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>{t('reporting.rejectionsByReason')}</h3>
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
          <h3 style={cardTitle}>{t('reporting.dailyTrend')}</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead><tr>{[
                t('reporting.dailyTrendTable.date'),
                t('reporting.dailyTrendTable.demand'),
                t('reporting.dailyTrendTable.allocations'),
                t('reporting.dailyTrendTable.rate'),
              ].map(h => <th key={h} style={th}>{h}</th>)}</tr></thead>
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

      {util.kind === 'ok' && util.data.items.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>{t('reporting.utilizationByLocation')}</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead><tr>{[
                t('reporting.utilizationTable.location'),
                t('reporting.utilizationTable.demand'),
                t('reporting.utilizationTable.allocated'),
                t('reporting.utilizationTable.rate'),
                t('reporting.utilizationTable.rejected'),
                t('reporting.utilizationTable.cancelled'),
                t('reporting.utilizationTable.noShows'),
              ].map(h => <th key={h} style={th}>{h}</th>)}</tr></thead>
              <tbody>
                {util.data.items.map(row => (
                  <tr key={row.locationId}>
                    <td style={td}>{displayLocation(row.locationId)}</td>
                    <td style={td}>{row.totalDemand}</td>
                    <td style={td}>{row.totalAllocations}</td>
                    <td style={td}>{(row.allocationRate * 100).toFixed(1)}%</td>
                    <td style={td}>{row.totalRejections}</td>
                    <td style={td}>{row.totalCancellations}</td>
                    <td style={td}>{row.totalNoShows}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
      {util.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>{t('reporting.utilizationError', { message: util.message })}</p>}

      {rc.kind === 'ok' && rc.data.items.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>{t('reporting.reasonCodes')}</h3>
          <p style={{ ...muted, marginTop: 0, marginBottom: 10 }}>{t('reporting.totalDemand', { count: rc.data.totalDemand })}</p>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead><tr>{[
                t('reporting.reasonCodeTable.reason'),
                t('reporting.reasonCodeTable.count'),
                t('reporting.reasonCodeTable.percentOfDemand'),
              ].map(h => <th key={h} style={th}>{h}</th>)}</tr></thead>
              <tbody>
                {rc.data.items.map(row => (
                  <tr key={row.reasonCode}>
                    <td style={td}>{row.reasonCode}</td>
                    <td style={td}>{row.count}</td>
                    <td style={td}>{(row.rateOfDemand * 100).toFixed(1)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
      {rc.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>{t('reporting.reasonCodesError', { message: rc.message })}</p>}

      {sum.kind === 'ok' && sum.data.items.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>{t('reporting.dailySummary')}</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead><tr>{[
                t('reporting.summaryTable.date'),
                t('reporting.summaryTable.location'),
                t('reporting.summaryTable.slot'),
                t('reporting.summaryTable.demand'),
                t('reporting.summaryTable.alloc'),
                t('reporting.summaryTable.rate'),
                t('reporting.summaryTable.rejected'),
                t('reporting.summaryTable.cancelled'),
                t('reporting.summaryTable.noShows'),
              ].map(h => <th key={h} style={th}>{h}</th>)}</tr></thead>
              <tbody>
                {sum.data.items.map((row, i) => (
                  <tr key={i}>
                    <td style={td}>{row.date}</td>
                    <td style={td}>{displayLocation(row.locationId)}</td>
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
      {sum.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>{t('reporting.summaryError', { message: sum.message })}</p>}

      {fair.kind === 'ok' && fair.data.items.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>{t('reporting.fairness')}</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead><tr>{[
                t('reporting.fairnessTable.requestor'),
                t('reporting.fairnessTable.requests'),
                t('reporting.fairnessTable.allocations'),
                t('reporting.fairnessTable.rate'),
              ].map(h => <th key={h} style={th}>{h}</th>)}</tr></thead>
              <tbody>
                {fair.data.items.map(row => (
                  <tr key={row.requestorRef}>
                    <td style={td}>{rowLabel(row.requestorRef)}</td>
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
      {fair.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>{t('reporting.fairnessError', { message: fair.message })}</p>}

      {empImpact.kind === 'ok' && empImpact.data.items.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>{t('reporting.employeeImpact')}</h3>
          <p style={{ ...muted, marginTop: 0, marginBottom: 10 }}>
            {t('reporting.employeeImpactSubtitle', { threshold: empImpact.data.minRejectionThreshold })}
          </p>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead><tr>{[
                t('reporting.employeeImpactTable.requestor'),
                t('reporting.employeeImpactTable.totalRequests'),
                t('reporting.employeeImpactTable.rejections'),
                t('reporting.employeeImpactTable.allocations'),
              ].map(h => <th key={h} style={th}>{h}</th>)}</tr></thead>
              <tbody>
                {empImpact.data.items.map(row => (
                  <tr key={row.requestorRef}>
                    <td style={td}>{rowLabel(row.requestorRef)}</td>
                    <td style={td}>{row.totalRequests}</td>
                    <td style={{ ...td, color: '#dc2626', fontWeight: 600 }}>{row.totalRejections}</td>
                    <td style={td}>{row.totalAllocations}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
      {empImpact.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>{t('reporting.employeeImpactError', { message: empImpact.message })}</p>}

      {ops.kind === 'ok' && ops.data.items.length > 0 && (
        <section style={card}>
          <h3 style={cardTitle}>{t('reporting.operationalExceptions')}</h3>
          <p style={{ ...muted, marginTop: 0, marginBottom: 10 }}>
            {t('reporting.operationalExceptionsSubtitle')}
          </p>
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead><tr>{[
                t('reporting.exceptionsTable.date'),
                t('reporting.exceptionsTable.location'),
                t('reporting.exceptionsTable.issue'),
                t('reporting.exceptionsTable.demand'),
                t('reporting.exceptionsTable.allocated'),
                t('reporting.exceptionsTable.rejected'),
              ].map(h => <th key={h} style={th}>{h}</th>)}</tr></thead>
              <tbody>
                {ops.data.items.map((row, i) => (
                  <tr key={i}>
                    <td style={td}>{formatDate(new Date(row.date + 'T00:00:00'), { dateStyle: 'medium' })}</td>
                    <td style={td}>{displayLocation(row.locationId) ?? row.locationId}</td>
                    <td style={{ ...td, color: '#b45309', fontWeight: 500 }}>{row.exceptionType === 'demand_no_allocations' ? t('reporting.exception.noAllocations') : row.exceptionType === 'failed_draw' ? row.description : t('reporting.exception.allRejected')}</td>
                    <td style={td}>{row.totalDemand}</td>
                    <td style={td}>{row.totalAllocations}</td>
                    <td style={{ ...td, color: '#dc2626' }}>{row.totalRejections}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
      {ops.kind === 'ok' && ops.data.items.length === 0 && (
        <section style={card}>
          <h3 style={cardTitle}>{t('reporting.operationalExceptions')}</h3>
          <p style={{ ...muted, margin: 0 }}>{t('reporting.noExceptions')}</p>
        </section>
      )}
      {ops.kind === 'error' && <p style={{ color: '#b91c1c', fontSize: 13 }}>{t('reporting.operationalExceptionsError', { message: ops.message })}</p>}
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
const btn: React.CSSProperties = { background: 'var(--brand-primary)', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
const btnSm: React.CSSProperties = { ...btn, background: '#374151', padding: '8px 14px', fontSize: 13 };
const tbl: React.CSSProperties = { width: '100%', borderCollapse: 'collapse', fontSize: 13 };
const th: React.CSSProperties = { textAlign: 'left', padding: '6px 8px', borderBottom: '1px solid #e5e7eb', color: '#6b7280', fontWeight: 500 };
const td: React.CSSProperties = { padding: '6px 8px', borderBottom: '1px solid #f3f4f6' };
