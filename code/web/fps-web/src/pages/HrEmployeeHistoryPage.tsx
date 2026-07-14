import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchHrEmployeeHistory,
  type HrEmployeeHistoryItem,
  type HrEmployeeHistorySummary,
} from '../api/bookings';
import { fetchHrDisplayNames } from '../api/profile';
import {
  displayDate,
  displayDateTime,
  displayLocation,
  displayRequestorRef,
  displaySlot,
  humanizeHrRejection,
} from '../displayLabels';
import { t, tDynamic, tPlural } from '../i18n';

const STATUS_FILTERS = ['All', 'Allocated', 'Pending', 'Rejected', 'Cancelled'];
const DEFAULT_WINDOW_DAYS = 30;

// Displayed label for a booking status code. The underlying value (used for
// state/filtering/API query params) always stays the English code.
function statusLabel(status: string): string {
  return tDynamic('hr.status', status, status);
}

type PageState =
  | { kind: 'loading' }
  | { kind: 'ok'; summary: HrEmployeeHistorySummary; items: HrEmployeeHistoryItem[]; totalCount: number }
  | { kind: 'forbidden' }
  | { kind: 'error'; message: string };

function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function nDaysAgoIso(n: number): string {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function statusBadgeStyle(status: string): React.CSSProperties {
  switch (status) {
    case 'Allocated': return { background: '#f0fdf4', color: '#166534', border: '1px solid #bbf7d0' };
    case 'Pending':   return { background: '#fffbeb', color: '#92400e', border: '1px solid #fcd34d' };
    case 'Cancelled':
    case 'NoShow':
    case 'Expired':   return { background: '#f9fafb', color: '#6b7280', border: '1px solid #e5e7eb' };
    case 'Rejected':  return { background: '#fef2f2', color: '#991b1b', border: '1px solid #fecaca' };
    default:          return { background: '#f9fafb', color: '#6b7280', border: '1px solid #e5e7eb' };
  }
}

export function HrEmployeeHistoryPage() {
  const { userId = '' } = useParams<{ userId: string }>();
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const from = searchParams.get('from') ?? nDaysAgoIso(DEFAULT_WINDOW_DAYS);
  const to = searchParams.get('to') ?? todayIso();
  const statusFilter = searchParams.get('status') ?? 'All';

  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [displayName, setDisplayName] = useState<string | null>(null);

  useEffect(() => {
    if (!userId) return;
    void fetchHrDisplayNames({ apiBaseUrl, bearerToken }, [userId]).then(result => {
      if (result.kind === 'ok') setDisplayName(result.data.names[userId] ?? null);
    });
  }, [apiBaseUrl, bearerToken, userId]);

  const load = useCallback(() => {
    if (!userId) return;
    setState({ kind: 'loading' });
    const status = statusFilter === 'All' ? undefined : statusFilter;
    fetchHrEmployeeHistory({ apiBaseUrl, bearerToken }, userId, { from, to, status }).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'forbidden') { setState({ kind: 'forbidden' }); return; }
      if (result.kind === 'ok') {
        setState({ kind: 'ok', summary: result.summary, items: result.items, totalCount: result.totalCount });
      } else {
        setState({ kind: 'error', message: 'message' in result ? result.message : t('hr.empHistory.loadError') });
      }
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, userId, from, to, statusFilter]);

  useEffect(() => { load(); }, [load]);

  const headerName = displayName ?? displayRequestorRef(userId);
  const shortRef = useMemo(() => {
    const clean = userId.replace(/-/g, '');
    return clean.length <= 6 ? clean.toUpperCase() : clean.slice(-6).toUpperCase();
  }, [userId]);

  function updateParam(key: string, value: string | null) {
    const next = new URLSearchParams(searchParams);
    if (value === null || value === '') next.delete(key);
    else next.set(key, value);
    setSearchParams(next, { replace: true });
  }

  return (
    <div className="page-stack">
      <div className="page-hero" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem' }}>
        <div style={{ minWidth: 0 }}>
          <h2 style={{ margin: 0, wordBreak: 'break-word' }}>{t('hr.empHistory.title')}</h2>
          <p>
            {t('hr.empHistory.headerLine', { name: headerName, ref: shortRef })}
          </p>
        </div>
        <Link to="/hr-operations" className="btn-secondary" style={{ flexShrink: 0, textDecoration: 'none' }}>
          {t('hr.empHistory.backLink')}
        </Link>
      </div>

      <div className="panel">
        {/* Filters */}
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '1rem' }}>
          <label style={{ display: 'flex', flexDirection: 'column', fontSize: '0.75rem', color: '#475569' }}>
            {t('hr.empHistory.from')}
            <input
              type="date"
              value={from}
              onChange={e => updateParam('from', e.target.value || null)}
              style={{ marginTop: 2, padding: '0.25rem 0.5rem', fontSize: '0.85rem', border: '1px solid #d1d5db', borderRadius: 4 }}
            />
          </label>
          <label style={{ display: 'flex', flexDirection: 'column', fontSize: '0.75rem', color: '#475569' }}>
            {t('hr.empHistory.to')}
            <input
              type="date"
              value={to}
              onChange={e => updateParam('to', e.target.value || null)}
              style={{ marginTop: 2, padding: '0.25rem 0.5rem', fontSize: '0.85rem', border: '1px solid #d1d5db', borderRadius: 4 }}
            />
          </label>
          <div style={{ display: 'flex', gap: '0.375rem', flexWrap: 'wrap' }}>
            {STATUS_FILTERS.map(s => (
              <button
                key={s}
                onClick={() => updateParam('status', s === 'All' ? null : s)}
                style={{ padding: '0.25rem 0.75rem', borderRadius: 12,
                  border: `1px solid ${statusFilter === s ? '#2563eb' : '#d1d5db'}`,
                  background: statusFilter === s ? '#eff6ff' : '#fff',
                  color: statusFilter === s ? '#2563eb' : '#374151',
                  fontSize: '0.8rem', cursor: 'pointer' }}
              >
                {statusLabel(s)}
              </button>
            ))}
          </div>
        </div>

        {/* Summary */}
        {state.kind === 'ok' && (
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
            <SummaryChip label={t('hr.empHistory.summary.total')} value={state.summary.total} />
            <SummaryChip label={t('hr.status.Allocated')} value={state.summary.allocated} tone="ok" />
            <SummaryChip label={t('hr.status.Pending')} value={state.summary.pending} tone="warn" />
            <SummaryChip label={t('hr.status.Rejected')} value={state.summary.rejected} tone="bad" />
            <SummaryChip label={t('hr.empHistory.summary.cancelledNoShow')} value={state.summary.cancelled} tone="muted" />
          </div>
        )}

        {/* Table */}
        {state.kind === 'loading' && <p style={{ color: '#6b7280', fontSize: '0.875rem' }}>{t('hr.empHistory.loading')}</p>}
        {state.kind === 'forbidden' && (
          <p style={{ color: '#991b1b', fontSize: '0.875rem' }}>{t('hr.empHistory.forbidden')}</p>
        )}
        {state.kind === 'error' && (
          <div>
            <p style={{ color: '#ef4444', fontSize: '0.875rem' }}>{state.message}</p>
            <button onClick={load} className="btn-primary">{t('hr.empHistory.retry')}</button>
          </div>
        )}
        {state.kind === 'ok' && state.items.length === 0 && (
          <div style={{ background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 6, padding: '1.5rem', textAlign: 'center' }}>
            <p style={{ color: '#6b7280', fontSize: '0.875rem', margin: 0 }}>
              {t('hr.empHistory.empty')}
            </p>
          </div>
        )}
        {state.kind === 'ok' && state.items.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid #e5e7eb', textAlign: 'left', color: '#64748b', fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                  <th style={{ padding: '0.5rem 0.5rem' }}>{t('hr.empHistory.col.date')}</th>
                  <th style={{ padding: '0.5rem 0.5rem' }}>{t('hr.empHistory.col.time')}</th>
                  <th style={{ padding: '0.5rem 0.5rem' }}>{t('hr.empHistory.col.location')}</th>
                  <th style={{ padding: '0.5rem 0.5rem' }}>{t('hr.empHistory.col.outcome')}</th>
                  <th style={{ padding: '0.5rem 0.5rem' }}>{t('hr.empHistory.col.spaceReason')}</th>
                  <th style={{ padding: '0.5rem 0.5rem' }}>{t('hr.empHistory.col.updated')}</th>
                </tr>
              </thead>
              <tbody>
                {state.items.map(item => (
                  <HistoryRow key={item.requestId} item={item} />
                ))}
              </tbody>
            </table>
            <p style={{ fontSize: '0.75rem', color: '#94a3b8', marginTop: '0.5rem' }}>
              {tPlural('hr.empHistory.matchCount', state.totalCount)}
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

function HistoryRow({ item }: { item: HrEmployeeHistoryItem }) {
  const timeWindow = item.timeSlotStart && item.timeSlotEnd
    ? `${item.timeSlotStart.slice(0, 5)}–${item.timeSlotEnd.slice(0, 5)}`
    : '—';
  const reasonText = (item.reasonCode || item.reason)
    ? humanizeHrRejection(item.reasonCode ?? null, item.reason ?? null)
    : null;
  const spaceText = item.allocatedSlotId
    ? (displaySlot(item.allocatedSlotId) ?? item.allocatedSlotId)
    : null;

  return (
    <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
      <td style={{ padding: '0.5rem' }}>{displayDate(item.requestedDate)}</td>
      <td style={{ padding: '0.5rem', color: '#475569' }}>{timeWindow}</td>
      <td style={{ padding: '0.5rem', color: '#475569' }}>
        {displayLocation(item.locationId) ?? item.locationId ?? '—'}
      </td>
      <td style={{ padding: '0.5rem' }}>
        <span style={{ fontSize: '0.75rem', fontWeight: 600, padding: '0.15rem 0.6rem', borderRadius: 12, ...statusBadgeStyle(item.status) }}>
          {statusLabel(item.status)}
        </span>
      </td>
      <td style={{ padding: '0.5rem', color: '#0f172a' }}>
        {spaceText && <span style={{ fontWeight: 600, color: '#166534' }}>{spaceText}</span>}
        {reasonText && <span style={{ color: '#92400e' }}>{reasonText}</span>}
        {!spaceText && !reasonText && <span style={{ color: '#94a3b8' }}>—</span>}
      </td>
      <td style={{ padding: '0.5rem', color: '#94a3b8', fontSize: '0.75rem', whiteSpace: 'nowrap' }}>
        {displayDateTime(item.lastStatusChangedAt)}
      </td>
    </tr>
  );
}

function SummaryChip({ label, value, tone }: { label: string; value: number; tone?: 'ok' | 'warn' | 'bad' | 'muted' }) {
  const toneStyle: React.CSSProperties = (() => {
    switch (tone) {
      case 'ok':    return { background: '#f0fdf4', borderColor: '#bbf7d0', color: '#166534' };
      case 'warn':  return { background: '#fffbeb', borderColor: '#fcd34d', color: '#92400e' };
      case 'bad':   return { background: '#fef2f2', borderColor: '#fecaca', color: '#991b1b' };
      case 'muted': return { background: '#f9fafb', borderColor: '#e5e7eb', color: '#6b7280' };
      default:      return { background: '#eff6ff', borderColor: '#bfdbfe', color: '#1d4ed8' };
    }
  })();
  return (
    <div style={{ padding: '0.5rem 0.875rem', borderRadius: 8, border: `1px solid ${toneStyle.borderColor}`,
      background: toneStyle.background, color: toneStyle.color, minWidth: 100 }}>
      <div style={{ fontSize: '0.7rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em', opacity: 0.85 }}>
        {label}
      </div>
      <div style={{ fontSize: '1.25rem', fontWeight: 700, marginTop: 2 }}>{value}</div>
    </div>
  );
}
