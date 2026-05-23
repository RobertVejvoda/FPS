import { useRef, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import {
  commitHrImport,
  previewHrImport,
  type HrImportCommitResult,
  type HrImportPreview,
} from '../api/hrImport';

type Phase =
  | { kind: 'idle' }
  | { kind: 'loading'; action: 'preview' | 'commit' }
  | { kind: 'preview'; file: File; data: HrImportPreview }
  | { kind: 'committed'; data: HrImportCommitResult }
  | { kind: 'error'; message: string };

const STATUS_COLOR: Record<string, string> = {
  Created: 'green',
  Updated: '#1d4ed8',
  Unchanged: '#6b7280',
  Rejected: '#b91c1c',
};

export function HrImportPage() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const fileRef = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });

  async function handlePreview() {
    const file = fileRef.current?.files?.[0];
    if (!file) return;
    setPhase({ kind: 'loading', action: 'preview' });
    const result = await previewHrImport({ apiBaseUrl, bearerToken }, file);
    if (result.kind === 'ok') {
      setPhase({ kind: 'preview', file, data: result.data });
    } else {
      setPhase({ kind: 'error', message: result.kind === 'unreachable' ? result.message : (result.kind === 'error' ? result.message : 'Authentication required.') });
    }
  }

  async function handleCommit() {
    if (phase.kind !== 'preview') return;
    setPhase({ kind: 'loading', action: 'commit' });
    const result = await commitHrImport({ apiBaseUrl, bearerToken }, phase.file);
    if (result.kind === 'ok') {
      setPhase({ kind: 'committed', data: result.data });
    } else {
      setPhase({ kind: 'error', message: result.kind === 'unreachable' ? result.message : (result.kind === 'error' ? result.message : 'Authentication required.') });
    }
  }

  return (
    <div>
      <h2 style={{ marginTop: 0, marginBottom: '1rem', fontSize: '1.1rem', fontWeight: 700 }}>HR Employee Import</h2>
      <p style={{ color: '#6b7280', fontSize: '0.875rem', marginBottom: '1.5rem' }}>
        Upload an employees CSV file matching the{' '}
        <a href="/docs/hr-import.md" style={{ color: '#1d4ed8' }}>DATA001 import contract</a>.
        Preview before committing.
      </p>

      <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', marginBottom: '1.5rem' }}>
        <input ref={fileRef} type="file" accept=".csv" style={{ fontSize: '0.875rem' }} />
        <button
          onClick={() => void handlePreview()}
          disabled={phase.kind === 'loading'}
          style={{ padding: '6px 14px', fontSize: '0.875rem', cursor: 'pointer', border: '1px solid #d1d5db', borderRadius: 6, background: '#f9fafb' }}
        >
          {phase.kind === 'loading' && (phase as { action: string }).action === 'preview' ? 'Previewing…' : 'Preview'}
        </button>
      </div>

      {phase.kind === 'error' && (
        <p style={{ color: '#b91c1c', fontSize: '0.875rem', marginBottom: '1rem' }}>
          {phase.message}
        </p>
      )}

      {phase.kind === 'preview' && (
        <>
          <div style={{ display: 'flex', gap: '1.5rem', marginBottom: '1rem', fontSize: '0.875rem' }}>
            <span style={{ color: 'green' }}>Created: {phase.data.created}</span>
            <span style={{ color: '#1d4ed8' }}>Updated: {phase.data.updated}</span>
            <span style={{ color: '#6b7280' }}>Unchanged: {phase.data.unchanged}</span>
            <span style={{ color: '#b91c1c' }}>Rejected: {phase.data.rejected}</span>
          </div>
          <div style={{ overflowX: 'auto', marginBottom: '1rem' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
              <thead>
                <tr style={{ background: '#f3f4f6' }}>
                  <th style={th}>Line</th>
                  <th style={th}>Subject</th>
                  <th style={th}>Status</th>
                  <th style={th}>Note</th>
                </tr>
              </thead>
              <tbody>
                {phase.data.rows.map(row => (
                  <tr key={row.lineNumber}>
                    <td style={td}>{row.lineNumber}</td>
                    <td style={td}>{row.externalSubject || '—'}</td>
                    <td style={{ ...td, color: STATUS_COLOR[row.status] ?? '#111827', fontWeight: 600 }}>{row.status}</td>
                    <td style={td}>{row.reason ?? ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {phase.data.rejected === 0 ? (
            <button
              onClick={() => void handleCommit()}
              style={{ padding: '7px 18px', fontSize: '0.875rem', cursor: 'pointer', background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, fontWeight: 600 }}
            >
              {`Commit ${phase.data.created + phase.data.updated} changes`}
            </button>
          ) : (
            <p style={{ fontSize: '0.875rem', color: '#92400e' }}>
              Fix {phase.data.rejected} rejected row(s) before committing.
            </p>
          )}
        </>
      )}

      {phase.kind === 'committed' && (
        <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 8, padding: '1rem', fontSize: '0.875rem' }}>
          <strong style={{ color: '#15803d' }}>Import complete.</strong>{' '}
          {phase.data.applied} row(s) applied
          {phase.data.rejected > 0 && `, ${phase.data.rejected} rejected`}.
          {phase.data.errors.length > 0 && (
            <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem', color: '#b91c1c' }}>
              {phase.data.errors.map((e, i) => <li key={i}>{e}</li>)}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}

const th: React.CSSProperties = { padding: '6px 10px', textAlign: 'left', fontWeight: 600, borderBottom: '1px solid #e5e7eb' };
const td: React.CSSProperties = { padding: '5px 10px', borderBottom: '1px solid #f3f4f6' };
