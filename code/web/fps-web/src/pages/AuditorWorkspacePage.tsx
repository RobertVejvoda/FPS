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
import { fetchDrawProgress, fetchBookingRequestDetail, type AuditorBookingRequestDetail } from '../api/dataHub';
import {
  humanizeAuditEventType,
  humanizeAuditAction,
  humanizeAuditResult,
  humanizeActivityCategory,
  humanizeActorType,
  humanizeEntityType,
  displayActorRef,
  displayLocation,
  displayDate,
  displayDateTime,
  displaySlot,
  humanizeHrRejection,
} from '../displayLabels';
import { useTenantDateContext } from '../hooks/useTenantDateBase';
import { DateFilter, type RangeFilterValue } from '../components/DateFilter';
import { DrawProgressPanel, type ProgressState } from '../components/DrawProgressPanel';
import { t, tDynamic, formatDate, formatDateTime, formatTime } from '../i18n';

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
  const fmt = (iso?: string) => iso ? formatDate(new Date(iso)) : '…';
  if (range.after && range.before) {
    if (range.after.slice(0, 10) === range.before.slice(0, 10)) return fmt(range.after);
    return `${fmt(range.after)} – ${fmt(range.before)}`;
  }
  return range.after
    ? t('audit.workspace.dateRange.from', { date: fmt(range.after) })
    : t('audit.workspace.dateRange.until', { date: fmt(range.before) });
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
  if (entityId.trim()) parts.push(t('audit.workspace.filterSummary.entityId', { value: entityId.trim() }));
  if (actorRef.trim()) parts.push(t('audit.workspace.filterSummary.actorRef', { value: actorRef.trim() }));
  if (result.trim()) parts.push(t('audit.workspace.filterSummary.result', { value: result.trim() }));
  if (parts.length === 0) return t('audit.workspace.noMatch');
  return t('audit.workspace.noRecordsFor', { parts: parts.join(', ') });
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

type BookingRequestDetailState =
  | { kind: 'loading' }
  | { kind: 'ok'; data: AuditorBookingRequestDetail }
  | { kind: 'notFound' }
  | { kind: 'error'; message: string };

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
  // Draw lifecycle progress cache — keyed by drawAttemptId (entityId).
  // Loaded on demand when the auditor expands a drawAttempt entity row.
  const [drawProgressCache, setDrawProgressCache] = useState<Record<string, ProgressState>>({});
  // Booking request detail cache — keyed by bookingRequestId (entityId).
  // Loaded on demand when the auditor opens a bookingRequest entity panel.
  const [bookingRequestDetailCache, setBookingRequestDetailCache] = useState<Record<string, BookingRequestDetailState>>({});
  // Actor detail panel: opened by clicking the "Who" cell.
  const [actorPanel, setActorPanel] = useState<{ actorHash: string; actorType: string; details: ActorDetails | undefined } | null>(null);
  // Entity detail panel: opened by clicking the "Entity ID" cell.
  const [entityPanel, setEntityPanel] = useState<AuditRecord | null>(null);

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
          message: 'message' in res ? res.message : t('audit.console.loadError'),
        });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, category, dateRange, dateBase, entityId, actorRef, result]);

  useEffect(() => {
    load();
  }, [load]);

  // Load Draw lifecycle progress for a drawAttempt entity on demand.
  // Cached per drawAttemptId; subsequent calls for the same ID are no-ops.
  function loadDrawProgress(drawAttemptId: string) {
    const cached = drawProgressCache[drawAttemptId];
    if (cached?.kind === 'ok' || cached?.kind === 'loading') return;
    setDrawProgressCache(prev => ({ ...prev, [drawAttemptId]: { kind: 'loading' } }));
    void fetchDrawProgress({ apiBaseUrl, bearerToken }, drawAttemptId).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setDrawProgressCache(prev => ({ ...prev, [drawAttemptId]: { kind: 'ok', data: result.data } }));
      } else {
        setDrawProgressCache(prev => ({
          ...prev,
          [drawAttemptId]: { kind: 'error', message: result.message },
        }));
      }
    });
  }

  // Load booking request detail for a bookingRequest entity on demand.
  // Cached per bookingRequestId; subsequent calls for the same ID are no-ops.
  function loadBookingRequestDetail(bookingRequestId: string) {
    const cached = bookingRequestDetailCache[bookingRequestId];
    if (cached?.kind === 'ok' || cached?.kind === 'loading') return;
    setBookingRequestDetailCache(prev => ({ ...prev, [bookingRequestId]: { kind: 'loading' } }));
    void fetchBookingRequestDetail({ apiBaseUrl, bearerToken }, bookingRequestId).then(res => {
      if (res.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (res.kind === 'error' && res.status === 404) {
        setBookingRequestDetailCache(prev => ({ ...prev, [bookingRequestId]: { kind: 'notFound' } }));
      } else if (res.kind === 'ok') {
        setBookingRequestDetailCache(prev => ({ ...prev, [bookingRequestId]: { kind: 'ok', data: res.data } }));
      } else {
        setBookingRequestDetailCache(prev => ({
          ...prev,
          [bookingRequestId]: { kind: 'error', message: res.message },
        }));
      }
    });
  }

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

  // Close any open detail panels when Escape is pressed.
  useEffect(() => {
    if (!actorPanel && !entityPanel) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { setActorPanel(null); setEntityPanel(null); }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [actorPanel, entityPanel]);

  // Open entity detail panel and auto-trigger draw progress load for
  // drawAttempt entities so the panel shows lifecycle context immediately.
  function openEntityPanel(record: AuditRecord) {
    setEntityPanel(record);
    if (record.entityType === 'drawAttempt' && record.entityId) {
      loadDrawProgress(record.entityId);
    }
    if (record.entityType === 'bookingRequest' && record.entityId) {
      loadBookingRequestDetail(record.entityId);
    }
  }

  // Open actor detail panel for the given audit record.
  function openActorPanel(record: AuditRecord, details: ActorDetails | undefined) {
    if (!record.actorHash) return;
    setActorPanel({ actorHash: record.actorHash, actorType: record.actorType, details });
  }

  const totalDisplayed = useMemo(
    () => state.kind === 'ok' ? state.records.length : 0,
    [state],
  );

  function exportCsv() {
    if (state.kind !== 'ok' || state.records.length === 0) return;
    const headers = [
      t('audit.workspace.csv.occurredAt'),
      t('audit.workspace.csv.eventType'),
      t('audit.workspace.csv.action'),
      t('audit.workspace.csv.entityType'),
      t('audit.workspace.csv.entityId'),
      t('audit.workspace.csv.actorType'),
      t('audit.workspace.csv.actorName'),
      t('audit.workspace.csv.actorReference'),
      t('audit.workspace.csv.actorHash'),
      t('audit.workspace.csv.result'),
      t('audit.workspace.csv.reasonCode'),
      t('audit.workspace.csv.summary'),
    ];
    const rows = state.records.map((r) => {
      const details = r.actorHash ? actorDetails[r.actorHash] : undefined;
      return [
        formatDateTime(new Date(r.occurredAt)),
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
        <h2 style={title}>{t('audit.workspace.title')}</h2>
        <p style={subtitle}>
          {t('audit.workspace.subtitle')}
        </p>
      </div>

      <section style={card}>
        <h3 style={cardTitle}>{t('audit.workspace.filtersTitle')}</h3>
        <div style={filterGrid}>
          <div style={filterItem}>
            <label style={label}>{t('audit.workspace.categoryLabel')}</label>
            <select value={category} onChange={(e) => setCategory(e.target.value as ActivityCategory)} style={select}>
              {ACTIVITY_CATEGORIES.map((cat) => (
                <option key={cat} value={cat}>
                  {humanizeActivityCategory(cat)}
                </option>
              ))}
            </select>
          </div>

          <div style={{ ...filterItem, gridColumn: '1 / -1' }}>
            <label style={label}>{t('audit.workspace.dateRangeLabel')}</label>
            <DateFilter
              mode="range"
              value={dateRange}
              onChange={setDateRange}
              dateBase={dateBase}
            />
          </div>

          <div style={filterItem}>
            <label style={label}>{t('audit.workspace.entityIdLabel')}</label>
            <input
              type="text"
              placeholder={t('audit.workspace.entityIdPlaceholder')}
              value={entityId}
              onChange={(e) => setEntityId(e.target.value)}
              style={input}
            />
            <span style={hint}>{t('audit.workspace.entityIdHint')}</span>
          </div>

          <div style={filterItem}>
            <label style={label}>{t('audit.workspace.actorRefLabel')}</label>
            <input
              type="text"
              placeholder={t('audit.workspace.actorRefPlaceholder')}
              value={actorRef}
              onChange={(e) => setActorRef(e.target.value.toUpperCase())}
              style={input}
            />
            <span style={hint}>{t('audit.workspace.actorRefHint')}</span>
          </div>

          <div style={filterItem}>
            <label style={label}>{t('audit.workspace.resultLabel')}</label>
            <input
              type="text"
              placeholder={t('audit.workspace.resultPlaceholder')}
              value={result}
              onChange={(e) => setResult(e.target.value)}
              style={input}
            />
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10, marginTop: 14 }}>
          <button onClick={load} style={btn}>
            {t('audit.common.refresh')}
          </button>
          <button onClick={exportCsv} disabled={state.kind !== 'ok' || state.records.length === 0} style={btnSecondary}>
            {t('audit.workspace.exportCsv')}
          </button>
        </div>
      </section>

      <section style={card}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <h3 style={{ ...cardTitle, margin: 0 }}>
            {t('audit.workspace.evidenceTitle')}
            {state.kind === 'ok' ? (
              <span style={{ ...muted, fontWeight: 400 }}> {t('audit.workspace.recordsCount', { count: state.totalCount })}</span>
            ) : null}
          </h3>
        </div>

        {state.kind === 'loading' && <p style={muted}>{t('audit.workspace.loadingRecords')}</p>}

        {state.kind === 'forbidden' && (
          <div style={errorBox}>
            <p style={{ margin: 0 }}>
              {t('audit.workspace.forbidden')}
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
            <p style={{ margin: '0 0 8px', fontWeight: 500 }}>{t('audit.workspace.emptyTitle')}</p>
            <p style={{ margin: 0, fontSize: 13, color: '#6b7280' }}>
              {hasActiveFilters
                ? buildEmptyStateMessage(category, dateRange, entityId, actorRef, result)
                : t('audit.workspace.emptyNoFilters')}
            </p>
          </div>
        )}

        {state.kind === 'ok' && state.records.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <p style={{ ...muted, marginTop: 0, marginBottom: 10 }}>
              {t('audit.workspace.showingCount', { shown: totalDisplayed, total: state.totalCount })}
            </p>
            <table style={table}>
              <thead>
                <tr>
                  {[
                    t('audit.workspace.table.when'),
                    t('audit.workspace.table.whatHappened'),
                    t('audit.workspace.table.action'),
                    t('audit.workspace.table.entity'),
                    t('audit.workspace.table.entityId'),
                    t('audit.workspace.table.who'),
                    t('audit.workspace.table.result'),
                    t('audit.workspace.table.reason'),
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
                          {formatDate(new Date(r.occurredAt))}
                        </div>
                        <div style={{ fontSize: 11, color: '#9ca3af', whiteSpace: 'nowrap' }}>
                          {formatTime(new Date(r.occurredAt))}
                        </div>
                      </td>
                      <td style={td}>{humanizeAuditEventType(r.eventType)}</td>
                      <td style={td}>{humanizeAuditAction(r.action)}</td>
                      <td style={td}>{humanizeEntityType(r.entityType)}</td>
                      <td
                        style={{ ...td, cursor: r.entityId ? 'pointer' : undefined }}
                        title={r.entityId ? t('audit.workspace.entityDetailsTitle') : undefined}
                        onClick={(e) => { if (r.entityId) { e.stopPropagation(); openEntityPanel(r); } }}
                      >
                        {renderEntityLabel(r.entityId)}
                      </td>
                      <td
                        style={{ ...td, cursor: r.actorHash ? 'pointer' : undefined }}
                        title={r.actorHash ? t('audit.workspace.actorDetailsTitle') : undefined}
                        onClick={(e) => { if (r.actorHash) { e.stopPropagation(); openActorPanel(r, details); } }}
                      >
                        {renderActorLabel(r.actorType, r.actorHash, details)}
                      </td>
                      <td style={td}>
                        <span style={resultBadge(r.result)}>{humanizeAuditResult(r.result)}</span>
                      </td>
                      <td style={{ ...td, color: '#6b7280', fontSize: 12 }}>{r.reasonCode ?? t('common.notAvailable')}</td>
                    </tr>
                    {isExpanded && (
                      <tr>
                        <td colSpan={8} style={{ ...td, padding: '14px 18px', background: '#f9fafb', borderBottom: '1px solid #e5e7eb' }}>
                          <ExpandedDetail
                            record={r}
                            actor={details}
                            drawProgress={r.entityType === 'drawAttempt' && r.entityId ? drawProgressCache[r.entityId] : undefined}
                            onLoadDrawProgress={r.entityType === 'drawAttempt' && r.entityId ? () => loadDrawProgress(r.entityId!) : undefined}
                          />
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
      {actorPanel && (
        <ActorDetailPanel
          actorHash={actorPanel.actorHash}
          actorType={actorPanel.actorType}
          details={actorPanel.details}
          onClose={() => setActorPanel(null)}
        />
      )}
      {entityPanel && (() => {
        const entityId = entityPanel.entityId;
        const isDrawAttemptWithId = entityPanel.entityType === 'drawAttempt' && entityId !== null;
        const isBookingRequestWithId = entityPanel.entityType === 'bookingRequest' && entityId !== null;
        return (
          <EntityDetailPanel
            record={entityPanel}
            drawProgress={isDrawAttemptWithId ? drawProgressCache[entityId as string] : undefined}
            onLoadDrawProgress={isDrawAttemptWithId ? () => loadDrawProgress(entityId as string) : undefined}
            bookingRequestDetail={isBookingRequestWithId ? bookingRequestDetailCache[entityId as string] : undefined}
            onLoadBookingRequestDetail={isBookingRequestWithId ? () => loadBookingRequestDetail(entityId as string) : undefined}
            onClose={() => setEntityPanel(null)}
          />
        );
      })()}
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
  if (!actorHash) return <span style={{ color: '#9ca3af' }}>{typeLabel} · {t('common.notAvailable')}</span>;
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
  if (!entityId) return <span style={{ color: '#9ca3af' }}>{t('common.notAvailable')}</span>;
  const short = entityId.length > 8 ? entityId.slice(0, 8) + '…' : entityId;
  return <span style={{ fontFamily: 'monospace', fontSize: 11, color: '#374151' }}>{short}</span>;
}

// Inline expansion: shown when the auditor clicks a row. Surfaces the full
// actor and entity references plus copy buttons so the values can be moved
// into the search fields without retyping. Intentionally keeps PII to a
// minimum — only what is needed to answer "who did this?" and "to what?".
// For drawAttempt entities, shows an inline lifecycle progress panel so
// auditors can review Draw evidence without leaving this workspace.
function ExpandedDetail({
  record,
  actor,
  drawProgress,
  onLoadDrawProgress,
}: {
  record: AuditRecord;
  actor: ActorDetails | undefined;
  drawProgress?: ProgressState;
  onLoadDrawProgress?: () => void;
}): React.ReactElement {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 14 }}>
      <div>
        <div style={detailLabel}>{t('audit.workspace.detail.actor')}</div>
        {actor?.displayName ? (
          <div style={{ fontWeight: 500, marginBottom: 2 }}>{actor.displayName}</div>
        ) : (
          <div style={{ color: '#9ca3af', marginBottom: 2 }}>{t('audit.workspace.detail.noProfileData')}</div>
        )}
        <div style={{ ...muted, fontFamily: 'monospace', display: 'flex', alignItems: 'center', gap: 6 }}>
          {t('audit.workspace.detail.shortRef')}
          <span style={detailValueChip}>{actor?.shortRef ?? displayActorRef(record.actorHash)}</span>
          {actor?.shortRef && <CopyButton value={actor.shortRef} label={t('audit.workspace.copyShortRef')} />}
        </div>
        <div style={{ ...muted, fontSize: 11, marginTop: 4 }}>{humanizeActorType(record.actorType)}</div>
      </div>
      <div>
        <div style={detailLabel}>{t('audit.workspace.detail.entity')}</div>
        <div style={{ marginBottom: 2 }}>{humanizeEntityType(record.entityType)}</div>
        {record.entityId ? (
          <div style={{ ...muted, fontFamily: 'monospace', display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
            {t('audit.workspace.detail.id')}
            <span style={detailValueChip}>{record.entityId}</span>
            <CopyButton value={record.entityId} label={t('audit.workspace.copyEntityId')} />
          </div>
        ) : (
          <div style={{ ...muted }}>{t('audit.workspace.detail.notAvailableForEvent')}</div>
        )}
      </div>
      {record.summary && (
        <div style={{ gridColumn: '1 / -1' }}>
          <div style={detailLabel}>{t('audit.workspace.detail.summary')}</div>
          <div>{record.summary}</div>
        </div>
      )}
      {record.entityType === 'drawAttempt' && record.entityId && (
        <div style={{ gridColumn: '1 / -1' }}>
          <div style={detailLabel}>{t('audit.workspace.detail.drawProgress')}</div>
          {!drawProgress ? (
            <button
              type="button"
              onClick={onLoadDrawProgress}
              style={{
                marginTop: 6,
                fontSize: 12,
                padding: '3px 10px',
                borderRadius: 4,
                border: '1px solid #d1d5db',
                background: '#f9fafb',
                color: '#374151',
                cursor: 'pointer',
              }}
            >
              {t('audit.workspace.viewLifecycleProgress')}
            </button>
          ) : (
            <div style={{ marginTop: 8 }}>
              <DrawProgressPanel progress={drawProgress} drawAttemptId={record.entityId} />
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// Actor detail panel — fixed-position overlay showing safe actor info
// (display name, classification, short ref, copy). No raw hash or PII.
function ActorDetailPanel({
  actorHash,
  actorType,
  details,
  onClose,
}: {
  actorHash: string;
  actorType: string;
  details: ActorDetails | undefined;
  onClose: () => void;
}): React.ReactElement {
  const shortRef = details?.shortRef ?? displayActorRef(actorHash);
  return (
    <div style={panelOverlay} onClick={onClose}>
      <div style={panelBoxSm} onClick={(e) => e.stopPropagation()}>
        <div style={panelHeader}>
          <span style={{ fontWeight: 600, fontSize: 14 }}>{t('audit.workspace.actorDetailTitle')}</span>
          <button type="button" onClick={onClose} style={panelCloseBtn} aria-label={t('audit.workspace.closeActorDetail')}>✕</button>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14, marginTop: 14 }}>
          <div>
            <div style={detailLabel}>{t('audit.workspace.detail.classification')}</div>
            <div style={{ fontSize: 14 }}>{humanizeActorType(actorType)}</div>
          </div>
          <div>
            <div style={detailLabel}>{t('audit.workspace.detail.displayName')}</div>
            {details?.displayName ? (
              <div style={{ fontSize: 14, fontWeight: 500 }}>{details.displayName}</div>
            ) : (
              <div style={{ ...muted, fontStyle: 'italic' }}>{t('audit.workspace.detail.notAvailable')}</div>
            )}
          </div>
          <div>
            <div style={detailLabel}>{t('audit.workspace.detail.shortRefLabel')}</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ ...detailValueChip, fontFamily: 'monospace', fontWeight: 600, fontSize: 13 }}>{shortRef}</span>
              <CopyButton value={shortRef} label={t('audit.workspace.copyActorShortRef')} />
            </div>
            <div style={{ ...muted, fontSize: 11, marginTop: 4 }}>
              {t('audit.workspace.shortRefHint')}
            </div>
          </div>
        </div>
        <div style={{ marginTop: 16, paddingTop: 12, borderTop: '1px solid #e5e7eb', fontSize: 11, color: '#9ca3af' }}>
          {t('audit.workspace.pseudonymisedNotice')}
        </div>
      </div>
    </div>
  );
}

// Entity detail panel — fixed-position overlay routing to appropriate
// context by entity type. Unsupported types show a graceful fallback with
// the entity type, copyable ID, and "No detail view available yet".
function EntityDetailPanel({
  record,
  drawProgress,
  onLoadDrawProgress,
  bookingRequestDetail,
  onLoadBookingRequestDetail,
  onClose,
}: {
  record: AuditRecord;
  drawProgress?: ProgressState;
  onLoadDrawProgress?: () => void;
  bookingRequestDetail?: BookingRequestDetailState;
  onLoadBookingRequestDetail?: () => void;
  onClose: () => void;
}): React.ReactElement {
  const entityTypeName = humanizeEntityType(record.entityType);
  const isDrawAttempt = record.entityType === 'drawAttempt';
  const isBookingRequest = record.entityType === 'bookingRequest';
  const hasDetailView = isDrawAttempt || isBookingRequest;
  return (
    <div style={panelOverlay} onClick={onClose}>
      <div style={(isDrawAttempt || isBookingRequest) ? panelBoxLg : panelBoxSm} onClick={(e) => e.stopPropagation()}>
        <div style={panelHeader}>
          <span style={{ fontWeight: 600, fontSize: 14 }}>{t('audit.workspace.entityDetailTitle', { entityType: entityTypeName })}</span>
          <button type="button" onClick={onClose} style={panelCloseBtn} aria-label={t('audit.workspace.closeEntityDetail')}>✕</button>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14, marginTop: 14 }}>
          <div>
            <div style={detailLabel}>{t('audit.workspace.detail.entityType')}</div>
            <div style={{ fontSize: 14 }}>{entityTypeName}</div>
          </div>
          {record.entityId && (
            <div>
              <div style={detailLabel}>{t('audit.workspace.detail.entityId')}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                <span style={entityIdChip}>
                  {record.entityId}
                </span>
                <CopyButton value={record.entityId} label={t('audit.workspace.copyEntityId')} />
              </div>
            </div>
          )}

          {isDrawAttempt && record.entityId && (
            <div>
              <div style={detailLabel}>{t('audit.workspace.detail.drawProgress')}</div>
              {drawProgress ? (
                <div style={{ marginTop: 8 }}>
                  <DrawProgressPanel progress={drawProgress} drawAttemptId={record.entityId} />
                </div>
              ) : (
                <button
                  type="button"
                  onClick={onLoadDrawProgress}
                  style={{ marginTop: 6, fontSize: 12, padding: '3px 10px', borderRadius: 4, border: '1px solid #d1d5db', background: '#f9fafb', color: '#374151', cursor: 'pointer' }}
                >
                  {t('audit.workspace.viewLifecycleProgress')}
                </button>
              )}
            </div>
          )}

          {isBookingRequest && record.entityId && (
            <div>
              <div style={detailLabel}>{t('audit.workspace.detail.bookingRequestDetails')}</div>
              <div style={{ marginTop: 8 }}>
                <BookingRequestDetailPanel
                  detail={bookingRequestDetail}
                  bookingRequestId={record.entityId}
                  onLoad={onLoadBookingRequestDetail}
                />
              </div>
            </div>
          )}

          {!hasDetailView && (
            <div style={{ padding: '10px 12px', background: '#f9fafb', borderRadius: 6, border: '1px solid #e5e7eb' }}>
              <p style={{ margin: '0 0 4px', fontWeight: 500, fontSize: 13 }}>{t('audit.workspace.noDetailView')}</p>
              <p style={{ ...muted, margin: 0, fontSize: 12 }}>
                {t('audit.workspace.noDetailViewDescription.prefix')}<strong>{entityTypeName}</strong>{t('audit.workspace.noDetailViewDescription.suffix')}
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// AUD008: Auditor-safe booking request detail panel.
// Shows business-context fields from the DataHub BookingOutcome projection.
// Auto-loads on first open; shows a load button when detail is undefined.
function BookingRequestDetailPanel({
  detail,
  bookingRequestId,
  onLoad,
}: {
  detail?: BookingRequestDetailState;
  bookingRequestId: string;
  onLoad?: () => void;
}): React.ReactElement {
  if (!detail) {
    return (
      <button
        type="button"
        onClick={onLoad}
        style={{ fontSize: 12, padding: '3px 10px', borderRadius: 4, border: '1px solid #d1d5db', background: '#f9fafb', color: '#374151', cursor: 'pointer' }}
      >
        {t('audit.workspace.viewRequestDetails')}
      </button>
    );
  }
  if (detail.kind === 'loading') {
    return <p style={{ margin: 0, color: 'var(--muted)', fontSize: '0.85rem' }}>{t('audit.workspace.loadingRequestDetails')}</p>;
  }
  if (detail.kind === 'notFound') {
    return (
      <div style={{ padding: '8px 10px', background: '#fefce8', borderRadius: 5, border: '1px solid #fde68a', fontSize: 13, color: '#92400e' }}>
        {t('audit.workspace.noProjectionFound')}
      </div>
    );
  }
  if (detail.kind === 'error') {
    return <p style={{ margin: 0, color: 'var(--danger)', fontSize: '0.85rem' }}>{detail.message}</p>;
  }

  const { data } = detail;

  const statusColors: Record<string, { bg: string; color: string; border: string }> = {
    Allocated: { bg: '#f0fdf4', color: '#166534', border: '#bbf7d0' },
    Rejected: { bg: '#fef2f2', color: '#991b1b', border: '#fecaca' },
    Cancelled: { bg: '#f9fafb', color: '#374151', border: '#e5e7eb' },
    Submitted: { bg: '#eff6ff', color: '#1d4ed8', border: '#bfdbfe' },
    Used: { bg: '#f0fdf4', color: '#14532d', border: '#86efac' },
    NoShow: { bg: '#fff7ed', color: '#9a3412', border: '#fed7aa' },
    Waitlisted: { bg: '#fefce8', color: '#854d0e', border: '#fde68a' },
    Expired: { bg: '#f9fafb', color: '#6b7280', border: '#e5e7eb' },
  };
  const statusStyle = statusColors[data.status] ?? statusColors.Submitted;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
      {/* Status badge */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
        <span style={{
          display: 'inline-block',
          padding: '2px 10px',
          borderRadius: 12,
          fontSize: 12,
          fontWeight: 600,
          background: statusStyle.bg,
          color: statusStyle.color,
          border: `1px solid ${statusStyle.border}`,
        }}>
          {tDynamic('audit.workspace.status', data.status, data.status)}
        </span>
      </div>

      {/* Requestor identity (safe short ref only — no raw userId) */}
      <div>
        <div style={{ fontSize: '0.7rem', color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.04em', marginBottom: 3 }}>{t('audit.workspace.detail.requestor')}</div>
        <span style={{ fontFamily: 'monospace', fontSize: 13, fontWeight: 600, letterSpacing: '0.05em', background: '#f1f5f9', padding: '2px 8px', borderRadius: 4, border: '1px solid #e2e8f0' }}>
          {data.requestorShortRef || t('common.notAvailable')}
        </span>
      </div>

      {/* Core facts grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '0.5rem 1.25rem' }}>
        <RequestFact label={t('audit.workspace.detail.locationFacility')} value={displayLocation(data.locationId) ?? data.locationId} />
        <RequestFact label={t('audit.workspace.detail.date')} value={displayDate(data.date)} />
        <RequestFact label={t('audit.workspace.detail.timeSlot')} value={data.timeSlot} />
        {data.slotId && (
          <RequestFact label={t('audit.workspace.detail.assignedSpace')} value={displaySlot(data.slotId) ?? data.slotId} />
        )}
        {data.allocationSource && (
          <RequestFact label={t('audit.workspace.detail.allocationSource')} value={data.allocationSource} />
        )}
      </div>

      {/* Vehicle facts */}
      {(data.vehicleLicensePlate || data.vehicleType) && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '0.5rem 1.25rem' }}>
          {data.vehicleLicensePlate && (
            <RequestFact label={t('audit.workspace.detail.licensePlate')} value={data.vehicleLicensePlate} />
          )}
          {data.vehicleType && (
            <RequestFact
              label={t('audit.workspace.detail.vehicleType')}
              value={data.vehicleIsElectric === true
                ? t('audit.workspace.detail.vehicleElectric', { vehicleType: data.vehicleType })
                : data.vehicleIsElectric === false
                  ? t('audit.workspace.detail.vehicleCombustion', { vehicleType: data.vehicleType })
                  : data.vehicleType}
            />
          )}
        </div>
      )}

      {/* Rejection / allocation reason */}
      {(data.reasonCode || data.safeReasonText) && (
        <div>
          <div style={{ fontSize: '0.7rem', color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.04em', marginBottom: 3 }}>{t('audit.workspace.detail.reason')}</div>
          <div style={{ fontSize: '0.85rem', color: '#374151' }}>
            {humanizeHrRejection(data.reasonCode, data.safeReasonText)}
          </div>
        </div>
      )}

      {/* Lifecycle timestamps */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '0.5rem 1.25rem' }}>
        {data.submittedAt && <RequestFact label={t('audit.workspace.detail.submitted')} value={displayDateTime(data.submittedAt)} />}
        {data.decidedAt && <RequestFact label={t('audit.workspace.detail.decided')} value={displayDateTime(data.decidedAt)} />}
      </div>

      {/* Draw link if available */}
      {data.drawAttemptId && (
        <div style={{ fontSize: '0.75rem', color: '#94a3b8' }}>
          {t('audit.workspace.detail.drawLabel')} <span style={{ fontFamily: 'monospace' }}>{data.drawAttemptId}</span>
        </div>
      )}

      {/* Support footer */}
      <div style={{ fontSize: '0.7rem', color: '#94a3b8', borderTop: '1px solid #e5e7eb', paddingTop: '0.5rem' }}>
        {t('audit.workspace.detail.requestIdLabel')} <span style={{ fontFamily: 'monospace' }}>{bookingRequestId}</span>
        {' · '}{t('audit.workspace.detail.projectedLabel', { date: displayDateTime(data.lastProjectedAt) })}
      </div>
    </div>
  );
}

function RequestFact({ label, value }: { label: string; value: string }): React.ReactElement {
  return (
    <div>
      <div style={{ fontSize: '0.7rem', color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>{label}</div>
      <div style={{ fontSize: '0.85rem', fontWeight: 500, color: '#0f172a' }}>{value}</div>
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
      {copied ? t('audit.workspace.copied') : t('audit.workspace.copy')}
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
// Entity ID chip: monospace font and word-wrap for long IDs in the entity detail panel.
const entityIdChip: React.CSSProperties = {
  ...detailValueChip,
  fontFamily: 'monospace',
  fontSize: 11,
  overflowWrap: 'break-word',
};
const panelOverlay: React.CSSProperties = {
  position: 'fixed',
  inset: 0,
  background: 'rgba(0,0,0,0.35)',
  zIndex: 1000,
  display: 'flex',
  alignItems: 'flex-start',
  justifyContent: 'flex-end',
  padding: '20px',
};
const panelBoxSm: React.CSSProperties = {
  background: '#fff',
  borderRadius: 10,
  boxShadow: '0 8px 32px rgba(0,0,0,0.18)',
  padding: '20px 24px',
  width: 340,
  maxWidth: '90vw',
  maxHeight: '80vh',
  overflowY: 'auto',
};
const panelBoxLg: React.CSSProperties = {
  background: '#fff',
  borderRadius: 10,
  boxShadow: '0 8px 32px rgba(0,0,0,0.18)',
  padding: '20px 24px',
  width: 520,
  maxWidth: '90vw',
  maxHeight: '80vh',
  overflowY: 'auto',
};
const panelHeader: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'space-between',
  borderBottom: '1px solid #e5e7eb',
  paddingBottom: 10,
};
const panelCloseBtn: React.CSSProperties = {
  background: 'none',
  border: 'none',
  cursor: 'pointer',
  fontSize: 16,
  color: '#6b7280',
  padding: '2px 6px',
  borderRadius: 4,
};
