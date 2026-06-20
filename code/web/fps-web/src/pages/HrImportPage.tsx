import { useRef, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import {
  commitHrImport,
  previewHrImport,
  type HrImportCommitResult,
  type HrImportPreview,
} from '../api/hrImport';

const HR_IMPORT_DOC_URL = 'https://github.com/RobertVejvoda/fairspot/blob/master/docs/hr-import.md';
const EMPLOYEE_HEADER = 'external_subject,display_name,email,roles,home_location,preferred_zone,parking_eligible,has_company_car,accessibility_eligible,reserved_space_eligible,active';
const EMPLOYEE_EXAMPLE = [
  EMPLOYEE_HEADER,
  'employee1,Jan Novak,jan.novak@example.invalid,employee,Prague,A,true,false,false,false,true',
  'employee2,Petra Svobodova,petra.svobodova@example.invalid,employee,Prague,,true,true,false,false,true',
  'hr-admin,Lucie Prochazkova,lucie.prochazkova@example.invalid,employee;hr_manager,Prague,,false,false,false,false,true',
].join('\n');

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
        Upload an employees CSV file, preview row-level changes, then commit. The current screen imports employee/profile facts only.
        Vehicle import is a separate contract for validation and future upload support.
        {' '}
        <a href={HR_IMPORT_DOC_URL} target="_blank" rel="noreferrer" style={{ color: '#1d4ed8' }}>Open import contract</a>.
      </p>

      <section style={helpPanel}>
        <h3 style={helpTitle}>Expected employees.csv format</h3>
        <p style={helpText}>
          Use comma-separated columns exactly as shown. Roles are separated with semicolons, for example <code>employee;hr_manager</code>.
          Do not include passwords, employee numbers, national IDs, salaries, tokens, or manager notes.
        </p>
        <pre style={codeBlock}>{EMPLOYEE_EXAMPLE}</pre>
        <p style={helpText}>
          Company-car and accessibility flags are HR-controlled facts. Employees cannot set these for themselves.
        </p>
      </section>

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
const helpPanel: React.CSSProperties = {
  border: '1px solid #e5e7eb',
  borderRadius: 8,
  background: '#f9fafb',
  padding: '0.875rem 1rem',
  marginBottom: '1.5rem',
};
const helpTitle: React.CSSProperties = { margin: '0 0 0.5rem', fontSize: '0.9rem', fontWeight: 700 };
const helpText: React.CSSProperties = { margin: '0.5rem 0', color: '#4b5563', fontSize: '0.8125rem', lineHeight: 1.45 };
const codeBlock: React.CSSProperties = {
  margin: '0.75rem 0',
  overflowX: 'auto',
  border: '1px solid #e5e7eb',
  borderRadius: 6,
  background: '#fff',
  padding: '0.75rem',
  color: '#111827',
  fontSize: '0.75rem',
  lineHeight: 1.5,
};
