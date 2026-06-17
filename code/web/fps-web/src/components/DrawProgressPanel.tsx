import React from 'react';
import type { DrawProgressResponse } from '../api/dataHub';
import { displayDateTime, formatLifecycleStepName, lifecycleStepStatusColor } from '../displayLabels';

export type ProgressState =
  | { kind: 'loading' }
  | { kind: 'ok'; data: DrawProgressResponse }
  | { kind: 'error'; message: string };

export function humanizeTriggerSource(source: string): string {
  const label: Record<string, string> = {
    manual: 'Manual',
    scheduled: 'Scheduled',
    recovery: 'Recovery',
    simulation: 'Simulation',
  };
  return label[source] ?? source;
}

// Operator-safe short ref derived from the TriggeredBy value. For long
// hex hashes we surface the first 6 chars uppercased — matches the audit
// workspace convention so an HR user can correlate by short ref. For
// short identifiers (e.g. "dapr-cron") we render them verbatim.
export function shortTriggeredByRef(value: string | null): string | null {
  if (!value) return null;
  const compact = value.replace(/-/g, '');
  if (/^[0-9a-f]{32,}$/i.test(compact)) return compact.slice(0, 6).toUpperCase();
  return value;
}

export function ProgressFact({ label, value }: { label: string; value: string }): React.ReactElement {
  return (
    <div>
      <div style={{ fontSize: '0.7rem', color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>{label}</div>
      <div style={{ fontSize: '0.85rem', fontWeight: 500, color: '#0f172a' }}>{value}</div>
    </div>
  );
}

// DrawProgressPanel renders the full lifecycle progress for a single Draw attempt.
// `progress` comes from a parent cache; it may be undefined (never fetched),
// loading, ok, or error. All display data comes from the API response; only
// `drawAttemptId` is accepted externally for the audit reference footer.
export function DrawProgressPanel({
  progress,
  drawAttemptId,
}: {
  progress: ProgressState | undefined;
  drawAttemptId: string;
}): React.ReactElement {
  if (!progress || progress.kind === 'loading') {
    return <p style={{ margin: 0, color: 'var(--muted)', fontSize: '0.85rem' }}>Loading progress…</p>;
  }

  if (progress.kind === 'error') {
    return <p style={{ margin: 0, color: 'var(--danger)', fontSize: '0.85rem' }}>{progress.message}</p>;
  }

  const { data } = progress;

  return (
    <div>
      {/* Summary row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '0.5rem 1.25rem', marginBottom: '1rem' }}>
        <ProgressFact label="Trigger" value={data.triggerSource ? humanizeTriggerSource(data.triggerSource) : '—'} />
        {data.triggeredBy && <ProgressFact label="Run by" value={shortTriggeredByRef(data.triggeredBy) ?? data.triggeredBy} />}
        {data.runReason && <ProgressFact label="Reason" value={`"${data.runReason}"`} />}
        <ProgressFact label="Started" value={data.startedAt ? displayDateTime(data.startedAt) : '—'} />
        {data.completedAt && <ProgressFact label="Completed" value={displayDateTime(data.completedAt)} />}
      </div>

      {/* Safe failure reason + guidance */}
      {data.status === 'Failed' && data.safeFailureReason && (
        <div style={{
          padding: '0.6rem 0.8rem',
          borderRadius: 6,
          background: '#fef2f2',
          border: '1px solid #fecaca',
          marginBottom: '0.75rem',
        }}>
          <p style={{ margin: '0 0 0.3rem', fontSize: '0.85rem', color: '#991b1b', fontWeight: 600 }}>
            Draw failed: {data.safeFailureReason}
          </p>
          <p style={{ margin: 0, fontSize: '0.8rem', color: '#b91c1c' }}>
            To retry this Draw, use the Retry Draw action in Upcoming Draws (HR Draws page). If the
            failure persists, contact your system administrator with the Draw attempt ID below.
          </p>
        </div>
      )}

      {/* Lifecycle steps */}
      {data.steps && data.steps.length > 0 ? (
        <div>
          <div style={{ fontSize: '0.75rem', color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.04em', marginBottom: '0.5rem' }}>
            Lifecycle progress
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.3rem' }}>
            {data.steps.map((step, i) => {
              const color = lifecycleStepStatusColor(step.status);
              const icon = step.status === 'Completed' ? '✓'
                : step.status === 'Failed' ? '✗'
                : step.status === 'InProgress' ? '…'
                : '○';
              return (
                <div key={i} style={{ display: 'flex', gap: '0.6rem', alignItems: 'flex-start' }}>
                  <span style={{ fontSize: '0.8rem', color, fontWeight: 700, minWidth: 14, marginTop: 1 }}>{icon}</span>
                  <div style={{ flex: 1 }}>
                    <span style={{ fontSize: '0.85rem', fontWeight: 600, color: '#0f172a' }}>
                      {formatLifecycleStepName(step.stepName)}
                    </span>
                    {step.summary && (
                      <span style={{ fontSize: '0.8rem', color: 'var(--muted)', marginLeft: '0.5rem' }}>
                        — {step.summary}
                      </span>
                    )}
                    {step.occurredAt && (
                      <span style={{ fontSize: '0.75rem', color: '#94a3b8', marginLeft: '0.5rem' }}>
                        {displayDateTime(step.occurredAt)}
                      </span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      ) : (
        <p style={{ margin: 0, fontSize: '0.8rem', color: 'var(--muted)' }}>
          {data.stepsNote ?? 'Lifecycle steps are not available for this Draw.'}
        </p>
      )}

      {/* Draw attempt ID for support/audit reference */}
      <div style={{ marginTop: '0.75rem', fontSize: '0.75rem', color: '#94a3b8' }}>
        Draw attempt ID: <span style={{ fontFamily: 'monospace' }}>{drawAttemptId}</span>
      </div>
    </div>
  );
}
