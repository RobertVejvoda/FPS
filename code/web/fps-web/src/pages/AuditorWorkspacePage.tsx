import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchAuditRecords,
  resolveAuditActorReferences,
  type AuditRecord,
  type ActivityCategory,
  type AuditQueryFilters,
} from '../api/audit';
import { fetchHrDisplayNames } from '../api/profile';
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

// Actor-resolution map used to surface names instead of opaque hashes.
// Built in two hops: audit hash → userId via /audit/actor-references, then
// userId → display name via /profile/hr/display-names. Either hop can come
// back empty; in those cases the table falls back to the short ref alone.
interface ActorDetails {
  shortRef: string;
  userId: string | null;
  displayName: string | null;
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
  const [actorDetails, setActorDetails] = useState<Record<string, ActorDetails>>({});
  const [expandedRowId, setExpandedRowId] = useState<string | null>(null);

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

  // Resolve actor refs → userIds → display names after the table loads.
  // Audit records never carry the raw userId (pseudonymisation is the whole
  // point), so the auditor sees "Name · A3F1B2" instead of an opaque hash.
  // Both lookups are best-effort: an unresolved hash falls back to short
  // ref only (issue #482).
  useEffect(() => {
    if (state.kind !== 'ok') return;
    const hashes = Array.from(new Set(
      state.records.map(r => r.actorHash).filter((h): h is string => !!h && !(h in actorDetails))
    ));
    if (hashes.length === 0) return;

    void (async () => {
      const refsRes = await resolveAuditActorReferences({ apiBaseUrl, bearerToken }, hashes);
      const refs = refsRes.kind === 'ok' ? refsRes.data.items : {};

      const userIds = Object.values(refs).map(r => r.userId);
      const namesRes = userIds.length > 0
        ? await fetchHrDisplayNames({ apiBaseUrl, bearerToken }, userIds)
        : null;
      const names = namesRes && namesRes.kind === 'ok' ? namesRes.data.names : {};

      setActorDetails(prev => {
        const next = { ...prev };
        for (const hash of hashes) {
          const ref = refs[hash];
          next[hash] = {
            shortRef: ref?.shortRef ?? displayActorRef(hash),
            userId: ref?.userId ?? null,
            displayName: ref ? (names[ref.userId] ?? null) : null,
          };
        }
        return next;
      });
    })();
  }, [state, apiBaseUrl, bearerToken, actorDetails]);

  const totalDisplayed = useMemo(
    () => state.kind === 'ok' ? state.records.length : 0,
    [state],
  );

  function exportCsv() {
    if (state.kind !== 'ok' || state.records.length === 0) return;
    const headers = [
      'Occurred At',
      'Event Type',
      'Action',
      'Entity Type',
      'Entity ID',
      'Actor Type',
      'Actor Name',
      'Actor Reference',
      'Actor Hash (Evidence)',
      'Result',
      'Reason Code',
      'Summary',
    ];
    const rows = state.records.map((r) => {
      const details = r.actorHash ? actorDetails[r.actorHash] : undefined;
      return [
        new Date(r.occurredAt).toLocaleString(),
        humanizeAuditEventType(r.eventType),
        humanizeAuditAction(r.action),
        humanizeEntityType(r.entityType),
        r.entityId ?? '',
        humanizeActorType(r.actorType),
        details?.displayName ?? '',
        details?.shortRef ?? displayActorRef(r.actorHash),
        r.actorHash ?? '',
        humanizeAuditResult(r.result),
        r.reasonCode ?? '',
        r.summary ?? '',
      ];
    });
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
              placeholder="Paste from the Entity column"
              value={entityId}
              onChange={(e) => setEntityId(e.target.value)}
              style={input}
            />
            <span style={hint}>Booking ID, draw attempt ID, etc.</span>
          </div>

          <div style={filterItem}>
            <label style={label}>Actor short ref</label>
            <input
              type="text"
              placeholder="Paste from the Who column (e.g. A3F1B2)"
              value={actorRef}
              onChange={(e) => setActorRef(e.target.value.toUpperCase())}
              style={input}
            />
            <span style={hint}>6-character ref shown next to each actor.</span>
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
            <p style={{ ...muted, marginTop: 0, marginBottom: 10 }}>
              Showing {totalDisplayed} of {state.totalCount}. Click a row to see the full actor and entity reference.
            </p>
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
                {state.records.map((r) => {
                  const isExpanded = expandedRowId === r.auditRecordId;
                  const details = r.actorHash ? actorDetails[r.actorHash] : undefined;
                  return (
                  <React.Fragment key={r.auditRecordId}>
                    <tr style={{ ...tr, cursor: 'pointer', background: isExpanded ? '#f9fafb' : undefined }}
                        onClick={() => setExpandedRowId(isExpanded ? null : r.auditRecordId)}>
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
                      <td style={td}>{renderEntityLabel(r.entityId)}</td>
                      <td style={td}>{renderActorLabel(r.actorType, r.actorHash, details)}</td>
                      <td style={td}>
                        <span style={resultBadge(r.result)}>{humanizeAuditResult(r.result)}</span>
                      </td>
                      <td style={{ ...td, color: '#6b7280', fontSize: 12 }}>{r.reasonCode ?? '—'}</td>
                    </tr>
                    {isExpanded && (
                      <tr>
                        <td colSpan={8} style={{ ...td, padding: '14px 18px', background: '#f9fafb', borderBottom: '1px solid #e5e7eb' }}>
                          <ExpandedDetail record={r} actor={details} />
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                );})}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

// Actor cell: name first when the Profile lookup succeeded, short ref
// second. Falls back to short ref alone when no profile data exists or
// resolution hasn't completed yet. The actor type label ("Employee",
// "HR Manager", …) prefixes the value so the auditor sees role context
// without needing to expand the row.
function renderActorLabel(actorType: string, actorHash: string | null, details: ActorDetails | undefined): React.ReactNode {
  const typeLabel = humanizeActorType(actorType);
  if (!actorHash) return <span style={{ color: '#9ca3af' }}>{typeLabel} · —</span>;
  const ref = details?.shortRef ?? displayActorRef(actorHash);
  if (details?.displayName) {
    return (
      <span>
        <span style={{ color: '#6b7280', fontSize: 12 }}>{typeLabel} ·</span>{' '}
        <span style={{ fontWeight: 500 }}>{details.displayName}</span>{' '}
        <span style={{ color: '#9ca3af', fontSize: 11, fontFamily: 'monospace' }}>{ref}</span>
      </span>
    );
  }
  return (
    <span>
      <span style={{ color: '#6b7280', fontSize: 12 }}>{typeLabel} ·</span>{' '}
      <span style={{ fontFamily: 'monospace', fontSize: 12, color: '#374151', letterSpacing: '0.05em' }}>{ref}</span>
    </span>
  );
}

// Entity ID cell: keep a tight 8-character preview in the table so the
// row stays readable; the full ID lives in the expand panel below.
function renderEntityLabel(entityId: string | null): React.ReactNode {
  if (!entityId) return <span style={{ color: '#9ca3af' }}>—</span>;
  const short = entityId.length > 8 ? entityId.slice(0, 8) + '…' : entityId;
  return <span style={{ fontFamily: 'monospace', fontSize: 11, color: '#374151' }}>{short}</span>;
}

// Inline expansion: shown when the auditor clicks a row. Surfaces the full
// actor and entity references plus copy buttons so the values can be moved
// into the search fields without retyping. Intentionally keeps PII to a
// minimum — only what is needed to answer "who did this?" and "to what?".
function ExpandedDetail({ record, actor }: { record: AuditRecord; actor: ActorDetails | undefined }): React.ReactElement {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 14 }}>
      <div>
        <div style={detailLabel}>Actor</div>
        {actor?.displayName ? (
          <div style={{ fontWeight: 500, marginBottom: 2 }}>{actor.displayName}</div>
        ) : (
          <div style={{ color: '#9ca3af', marginBottom: 2 }}>No profile data available</div>
        )}
        <div style={{ ...muted, fontFamily: 'monospace', display: 'flex', alignItems: 'center', gap: 6 }}>
          Short ref:
          <span style={detailValueChip}>{actor?.shortRef ?? displayActorRef(record.actorHash)}</span>
          {actor?.shortRef && <CopyButton value={actor.shortRef} label="copy short ref" />}
        </div>
        <div style={{ ...muted, fontSize: 11, marginTop: 4 }}>{humanizeActorType(record.actorType)}</div>
      </div>
      <div>
        <div style={detailLabel}>Entity</div>
        <div style={{ marginBottom: 2 }}>{humanizeEntityType(record.entityType)}</div>
        {record.entityId ? (
          <div style={{ ...muted, fontFamily: 'monospace', display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
            ID:
            <span style={detailValueChip}>{record.entityId}</span>
            <CopyButton value={record.entityId} label="copy entity id" />
          </div>
        ) : (
          <div style={{ ...muted }}>Not available for this event</div>
        )}
      </div>
      {record.summary && (
        <div style={{ gridColumn: '1 / -1' }}>
          <div style={detailLabel}>Summary</div>
          <div>{record.summary}</div>
        </div>
      )}
    </div>
  );
}

function CopyButton({ value, label }: { value: string; label: string }): React.ReactElement {
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      onClick={async (e) => {
        e.stopPropagation();
        try {
          await navigator.clipboard.writeText(value);
          setCopied(true);
          setTimeout(() => setCopied(false), 1500);
        } catch {
          /* clipboard unavailable; ignore */
        }
      }}
      aria-label={label}
      style={{
        fontSize: 11,
        padding: '2px 8px',
        borderRadius: 4,
        border: '1px solid #d1d5db',
        background: '#fff',
        color: '#374151',
        cursor: 'pointer',
      }}
    >
      {copied ? 'Copied' : 'Copy'}
    </button>
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
const hint: React.CSSProperties = { fontSize: 11, color: '#6b7280', marginTop: 3 };
const detailLabel: React.CSSProperties = {
  fontSize: 11,
  fontWeight: 600,
  textTransform: 'uppercase',
  letterSpacing: '0.05em',
  color: '#6b7280',
  marginBottom: 4,
};
const detailValueChip: React.CSSProperties = {
  background: '#fff',
  padding: '2px 8px',
  border: '1px solid #e5e7eb',
  borderRadius: 4,
  fontSize: 12,
};
