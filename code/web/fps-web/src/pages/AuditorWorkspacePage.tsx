import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchAuditRecords,
  type AuditRecord,
  type ActivityCategory,
  type AuditQueryFilters,
} from '../api/audit';
import {
  humanizeAuditEventType,
  humanizeAuditAction,
  humanizeAuditResult,
  humanizeActivityCategory,
  humanizeActorType,
  humanizeEntityType,
  displayActorRef,
} from '../displayLabels';
import { useTenantDateContext } from '../hooks/useTenantDateBase';
import { DateFilter, type RangeFilterValue } from '../components/DateFilter';

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; records: AuditRecord[]; totalCount: number }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

const ACTIVITY_CATEGORIES: ActivityCategory[] = [
  'All',
  'BookingLifecycle',
  'DrawEvents',
  'PolicyChanges',
  'Notifications',
  'PrivacyErasure',
  'ManualCorrections',
];

// Date-range presets and bound math live in the shared <DateFilter /> now
// (issue #476). The page just keeps the resolved { after, before } and
// derives a friendly label from it for the empty-state message below.

function describeDateRange(range: RangeFilterValue): string | null {
  if (!range.after && !range.before) return null;
  const fmt = (iso?: string) => iso ? new Date(iso).toLocaleDateString() : '…';
  if (range.after && range.before) {
    if (range.after.slice(0, 10) === range.before.slice(0, 10)) return fmt(range.after);
    return `${fmt(range.after)} – ${fmt(range.before)}`;
  }
  return range.after ? `from ${fmt(range.after)}` : `until ${fmt(range.before)}`;
}

function buildEmptyStateMessage(
  category: ActivityCategory,
  range: RangeFilterValue,
  entityId: string,
  actorRef: string,
  result: string,
): string {
  const parts: string[] = [];
  if (category !== 'All') parts.push(humanizeActivityCategory(category).toLowerCase());
  const rangeLabel = describeDateRange(range);
  if (rangeLabel) parts.push(rangeLabel);
  if (entityId.trim()) parts.push(`entity ID "${entityId.trim()}"`);
  if (actorRef.trim()) parts.push(`actor reference "${actorRef.trim()}"`);
  if (result.trim()) parts.push(`result "${result.trim()}"`);
  if (parts.length === 0) return 'No records match the current filters.';
  return `No records found for ${parts.join(', ')}. Try a broader date range or clear some filters.`;
}

export function AuditorWorkspacePage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const { dateBase } = useTenantDateContext();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [category, setCategory] = useState<ActivityCategory>('All');
  // Initial state is the "All time" preset (not bare `{}`), so the chip
  // is visibly highlighted from first render — matching the previous
  // page behaviour where dateRange === '' selected the "All time" button.
  const [dateRange, setDateRange] = useState<RangeFilterValue>({ presetKey: 'All' });
  const [entityId, setEntityId] = useState('');
  const [actorRef, setActorRef] = useState('');
  const [result, setResult] = useState('');

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    const filters: AuditQueryFilters = {
      category: category === 'All' ? undefined : category,
      occurredAfter: dateRange.after,
      occurredBefore: dateRange.before,
      entityId: entityId.trim() || undefined,
      actorRef: actorRef.trim() || undefined,
      result: result.trim() || undefined,
      pageSize: 100,
    };
    fetchAuditRecords({ apiBaseUrl, bearerToken }, filters).then((res) => {
      if (res.kind === 'unauthenticated') {
        clear();
        navigate('/session');
        return;
      }
      if (res.kind === 'error' && res.status === 403) {
        setState({ kind: 'forbidden' });
        return;
      }
      if (res.kind === 'ok')
        setState({ kind: 'ok', records: res.data.items, totalCount: res.data.totalCount });
      else
        setState({
          kind: 'error',
          message: 'message' in res ? res.message : 'Failed to load audit records.',
        });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, category, dateRange, dateBase, entityId, actorRef, result]);

  useEffect(() => {
    load();
  }, [load]);

  function exportCsv() {
    if (state.kind !== 'ok' || state.records.length === 0) return;
    const headers = [
      'Occurred At',
      'Event Type',
      'Action',
      'Entity Type',
      'Entity ID',
      'Actor Type',
      'Actor Reference',
      'Actor Hash (Evidence)',
      'Result',
      'Reason Code',
      'Summary',
    ];
    const rows = state.records.map((r) => [
      new Date(r.occurredAt).toLocaleString(),
      humanizeAuditEventType(r.eventType),
      humanizeAuditAction(r.action),
      humanizeEntityType(r.entityType),
      r.entityId ?? '',
      humanizeActorType(r.actorType),
      displayActorRef(r.actorHash),
      r.actorHash ?? '',
      humanizeAuditResult(r.result),
      r.reasonCode ?? '',
      r.summary ?? '',
    ]);
    const csv = [headers, ...rows].map((row) => row.map((cell) => `"${cell}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `audit-evidence-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  const hasActiveFilters = !!(dateRange.after || dateRange.before || entityId.trim() || actorRef.trim() || result.trim() || category !== 'All');

  return (
    <div style={page}>
      <div style={header}>
        <h2 style={title}>Auditor System Activity Workspace</h2>
        <p style={subtitle}>
          Business-readable evidence of system activity, including booking lifecycle, Draw events, policy
          changes, and notifications.
        </p>
      </div>

      <section style={card}>
        <h3 style={cardTitle}>Filters</h3>
        <div style={filterGrid}>
          <div style={filterItem}>
            <label style={label}>Activity Category</label>
            <select value={category} onChange={(e) => setCategory(e.target.value as ActivityCategory)} style={select}>
              {ACTIVITY_CATEGORIES.map((cat) => (
                <option key={cat} value={cat}>
                  {humanizeActivityCategory(cat)}
                </option>
              ))}
            </select>
          </div>

          <div style={{ ...filterItem, gridColumn: '1 / -1' }}>
            <label style={label}>Date Range</label>
            <DateFilter
              mode="range"
              value={dateRange}
              onChange={setDateRange}
              dateBase={dateBase}
            />
          </div>

          <div style={filterItem}>
            <label style={label}>Entity ID</label>
            <input
              type="text"
              placeholder="e.g. booking ID, draw ID"
              value={entityId}
              onChange={(e) => setEntityId(e.target.value)}
              style={input}
            />
          </div>

          <div style={filterItem}>
            <label style={label}>Actor reference</label>
            <input
              type="text"
              placeholder="e.g. A3F1B2"
              value={actorRef}
              onChange={(e) => setActorRef(e.target.value.toUpperCase())}
              style={input}
            />
          </div>

          <div style={filterItem}>
            <label style={label}>Result</label>
            <input
              type="text"
              placeholder="e.g. allocated, rejected"
              value={result}
              onChange={(e) => setResult(e.target.value)}
              style={input}
            />
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10, marginTop: 14 }}>
          <button onClick={load} style={btn}>
            Refresh
          </button>
          <button onClick={exportCsv} disabled={state.kind !== 'ok' || state.records.length === 0} style={btnSecondary}>
            Export CSV
          </button>
        </div>
      </section>

      <section style={card}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <h3 style={{ ...cardTitle, margin: 0 }}>
            System Activity Evidence
            {state.kind === 'ok' ? (
              <span style={{ ...muted, fontWeight: 400 }}> ({state.totalCount} records)</span>
            ) : null}
          </h3>
        </div>

        {state.kind === 'loading' && <p style={muted}>Loading activity records…</p>}

        {state.kind === 'forbidden' && (
          <div style={errorBox}>
            <p style={{ margin: 0 }}>
              You do not have permission to access the auditor workspace. This workspace is restricted to
              auditor and administrator roles.
            </p>
          </div>
        )}

        {state.kind === 'error' && (
          <div style={errorBox}>
            <p style={{ margin: 0 }}>{state.message}</p>
          </div>
        )}

        {state.kind === 'ok' && state.records.length === 0 && (
          <div style={emptyBox}>
            <p style={{ margin: '0 0 8px', fontWeight: 500 }}>No activity records found</p>
            <p style={{ margin: 0, fontSize: 13, color: '#6b7280' }}>
              {hasActiveFilters
                ? buildEmptyStateMessage(category, dateRange, entityId, actorRef, result)
                : 'No audit records exist in the system yet. Activity evidence will appear here after booking requests, Draw events, policy changes, or other system actions occur.'}
            </p>
          </div>
        )}

        {state.kind === 'ok' && state.records.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <table style={table}>
              <thead>
                <tr>
                  {[
                    'When',
                    'What Happened',
                    'Action',
                    'Entity',
                    'Entity ID',
                    'Who',
                    'Actor Ref',
                    'Result',
                    'Reason',
                  ].map((h) => (
                    <th key={h} style={th}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {state.records.map((r) => (
                  <tr key={r.auditRecordId} style={tr}>
                    <td style={td}>
                      <div style={{ fontSize: 12, whiteSpace: 'nowrap' }}>
                        {new Date(r.occurredAt).toLocaleDateString()}
                      </div>
                      <div style={{ fontSize: 11, color: '#9ca3af', whiteSpace: 'nowrap' }}>
                        {new Date(r.occurredAt).toLocaleTimeString()}
                      </div>
                    </td>
                    <td style={td}>{humanizeAuditEventType(r.eventType)}</td>
                    <td style={td}>{humanizeAuditAction(r.action)}</td>
                    <td style={td}>{humanizeEntityType(r.entityType)}</td>
                    <td style={{ ...td, fontFamily: 'monospace', fontSize: 11, color: '#6b7280' }}>
                      {r.entityId ? r.entityId.slice(0, 8) + '…' : '—'}
                    </td>
                    <td style={td}>{humanizeActorType(r.actorType)}</td>
                    <td style={{ ...td, fontFamily: 'monospace', fontSize: 12, color: '#374151', letterSpacing: '0.05em' }}>
                      {displayActorRef(r.actorHash)}
                    </td>
                    <td style={td}>
                      <span style={resultBadge(r.result)}>{humanizeAuditResult(r.result)}</span>
                    </td>
                    <td style={{ ...td, color: '#6b7280', fontSize: 12 }}>{r.reasonCode ?? '—'}</td>
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

function resultBadge(result: string | null): React.CSSProperties {
  const baseStyle: React.CSSProperties = {
    display: 'inline-block',
    padding: '2px 8px',
    borderRadius: 4,
    fontSize: 11,
    fontWeight: 500,
  };

  if (!result) return { ...baseStyle, background: '#f3f4f6', color: '#6b7280' };

  const successResults = ['accepted', 'allocated', 'completed', 'confirmed', 'applied', 'updated'];
  const failureResults = ['rejected', 'failed', 'cancelled', 'expired'];
  const infoResults = ['started', 'recorded'];

  if (successResults.includes(result))
    return { ...baseStyle, background: '#dcfce7', color: '#166534', border: '1px solid #bbf7d0' };
  if (failureResults.includes(result))
    return { ...baseStyle, background: '#fee2e2', color: '#b91c1c', border: '1px solid #fecaca' };
  if (infoResults.includes(result))
    return { ...baseStyle, background: '#dbeafe', color: '#1e40af', border: '1px solid #bfdbfe' };

  return { ...baseStyle, background: '#f3f4f6', color: '#6b7280' };
}

const page: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 16 };
const header: React.CSSProperties = { marginBottom: 4 };
const title: React.CSSProperties = { margin: 0, fontSize: 22, fontWeight: 700 };
const subtitle: React.CSSProperties = { margin: '6px 0 0', fontSize: 14, color: '#6b7280', lineHeight: 1.5 };
const card: React.CSSProperties = {
  background: '#fff',
  border: '1px solid #e5e7eb',
  borderRadius: 8,
  padding: '16px 20px',
};
const cardTitle: React.CSSProperties = { margin: '0 0 10px', fontSize: 15, fontWeight: 700 };
const label: React.CSSProperties = { display: 'block', fontSize: 13, fontWeight: 500, color: '#374151', marginBottom: 4 };
const filterGrid: React.CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
  gap: 12,
};
const filterItem: React.CSSProperties = { display: 'flex', flexDirection: 'column' };
const input: React.CSSProperties = {
  border: '1px solid #d1d5db',
  borderRadius: 6,
  padding: '7px 10px',
  fontSize: 14,
  outline: 'none',
};
const select: React.CSSProperties = {
  border: '1px solid #d1d5db',
  borderRadius: 6,
  padding: '7px 10px',
  fontSize: 14,
  outline: 'none',
  background: '#fff',
};
const btn: React.CSSProperties = {
  background: '#1d4ed8',
  color: '#fff',
  border: 'none',
  borderRadius: 6,
  padding: '8px 16px',
  fontSize: 14,
  fontWeight: 500,
  cursor: 'pointer',
};
const btnSecondary: React.CSSProperties = {
  background: '#f3f4f6',
  color: '#374151',
  border: '1px solid #d1d5db',
  borderRadius: 6,
  padding: '8px 16px',
  fontSize: 14,
  fontWeight: 500,
  cursor: 'pointer',
};
const muted: React.CSSProperties = { color: '#6b7280', fontSize: 13 };
const errorBox: React.CSSProperties = {
  padding: '12px 16px',
  borderRadius: 6,
  background: '#fef2f2',
  border: '1px solid #fecaca',
  color: '#b91c1c',
  fontSize: 14,
};
const emptyBox: React.CSSProperties = {
  padding: '24px 20px',
  borderRadius: 6,
  background: '#f9fafb',
  border: '1px solid #e5e7eb',
  textAlign: 'center',
};
const table: React.CSSProperties = { width: '100%', borderCollapse: 'collapse', fontSize: 13 };
const th: React.CSSProperties = {
  textAlign: 'left',
  padding: '8px 10px',
  borderBottom: '2px solid #e5e7eb',
  color: '#374151',
  fontWeight: 600,
  fontSize: 12,
  whiteSpace: 'nowrap',
};
const tr: React.CSSProperties = { borderBottom: '1px solid #f3f4f6' };
const td: React.CSSProperties = { padding: '10px 10px', verticalAlign: 'top' };
