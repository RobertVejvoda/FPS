import { useRef, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import {
  commitHrImport,
  previewHrImport,
  type HrImportCommitResult,
  type HrImportPreview,
  type HrVehicleImportStatus,
} from '../api/hrImport';

const HR_IMPORT_DOC_URL = 'https://github.com/RobertVejvoda/fairspot/blob/master/docs/hr-import.md';
const EMPLOYEE_HEADER = 'external_subject,display_name,email,roles,home_location,preferred_zone,parking_eligible,has_company_car,accessibility_eligible,reserved_space_eligible,active';
const EMPLOYEE_EXAMPLE = [
  EMPLOYEE_HEADER,
  'employee1,Jan Novak,jan.novak@example.invalid,employee,Prague,A,true,false,false,false,true',
  'employee2,Petra Svobodova,petra.svobodova@example.invalid,employee,Prague,,true,true,false,false,true',
  'hr-admin,Lucie Prochazkova,lucie.prochazkova@example.invalid,employee;hr_manager,Prague,,false,false,false,false,true',
].join('\n');

const VEHICLE_HEADER = 'external_subject,vehicle_alias,vehicle_license_plate,vehicle_type,vehicle_is_electric,active';
const VEHICLE_EXAMPLE = [
  VEHICLE_HEADER,
  'employee1,Daily Driver,1AA 2345,car,false,true',
  'employee2,Company Fleet,3AC 4567,car,false,true',
].join('\n');

type Phase =
  | { kind: 'idle' }
  | { kind: 'loading'; action: 'preview' | 'commit' }
  | { kind: 'preview'; employees: File; vehicles?: File; data: HrImportPreview }
  | { kind: 'committed'; data: HrImportCommitResult }
  | { kind: 'error'; message: string };

const STATUS_COLOR: Record<string, string> = {
  Created: 'green',
  Updated: '#1d4ed8',
  Unchanged: '#6b7280',
  Rejected: '#b91c1c',
};

const VEHICLE_STATUS_COLOR: Record<HrVehicleImportStatus, string> = {
  Valid: 'green',
  Rejected: '#b91c1c',
};

export function HrImportPage() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const empFileRef = useRef<HTMLInputElement>(null);
  const vehFileRef = useRef<HTMLInputElement>(null);
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });

  async function handlePreview() {
    const employees = empFileRef.current?.files?.[0];
    if (!employees) return;
    const vehicles = vehFileRef.current?.files?.[0];
    setPhase({ kind: 'loading', action: 'preview' });
    const result = await previewHrImport({ apiBaseUrl, bearerToken }, employees, vehicles);
    if (result.kind === 'ok') {
      setPhase({ kind: 'preview', employees, vehicles, data: result.data });
    } else {
      setPhase({ kind: 'error', message: result.kind === 'unreachable' ? result.message : result.kind === 'error' ? result.message : 'Authentication required.' });
    }
  }

  async function handleCommit() {
    if (phase.kind !== 'preview') return;
    setPhase({ kind: 'loading', action: 'commit' });
    const result = await commitHrImport({ apiBaseUrl, bearerToken }, phase.employees, phase.vehicles);
    if (result.kind === 'ok') {
      setPhase({ kind: 'committed', data: result.data });
    } else {
      setPhase({ kind: 'error', message: result.kind === 'unreachable' ? result.message : result.kind === 'error' ? result.message : 'Authentication required.' });
    }
  }

  const isLoading = phase.kind === 'loading';
  const isPreviewLoading = isLoading && (phase as { action: string }).action === 'preview';
  const isCommitLoading = isLoading && (phase as { action: string }).action === 'commit';

  return (
    <div>
      <h2 style={{ marginTop: 0, marginBottom: '1rem', fontSize: '1.1rem', fontWeight: 700 }}>HR Import</h2>
      <p style={{ color: '#6b7280', fontSize: '0.875rem', marginBottom: '1.5rem' }}>
        Upload an employees CSV (required) and optionally a vehicles CSV, preview row-level changes, then commit.{' '}
        <a href={HR_IMPORT_DOC_URL} target="_blank" rel="noreferrer" style={{ color: '#1d4ed8' }}>Open import contract</a>.
      </p>

      <section style={helpPanel}>
        <h3 style={helpTitle}>employees.csv format (required)</h3>
        <p style={helpText}>
          Comma-separated columns. Roles use semicolons, for example <code>employee;hr_manager</code>.
          Do not include passwords, employee numbers, national IDs, salaries, tokens, or manager notes.
          Company-car and accessibility flags are HR-controlled and cannot be self-set by employees.
        </p>
        <pre style={codeBlock}>{EMPLOYEE_EXAMPLE}</pre>
      </section>

      <section style={{ ...helpPanel, marginTop: '0.5rem' }}>
        <h3 style={helpTitle}>vehicles.csv format (optional)</h3>
        <p style={helpText}>
          Each row links a vehicle to an employee. The <code>external_subject</code> must match a subject
          in the employees file or an existing profile. One employee may have multiple vehicle rows.
          Valid <code>vehicle_type</code> values: <code>car</code>, <code>motorcycle</code>, <code>van</code>.
        </p>
        <pre style={codeBlock}>{VEHICLE_EXAMPLE}</pre>
      </section>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1.5rem' }}>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
          <label style={{ fontSize: '0.875rem', minWidth: 120, color: '#374151' }}>
            <strong>employees.csv</strong>
          </label>
          <input ref={empFileRef} type="file" accept=".csv" style={{ fontSize: '0.875rem' }} />
        </div>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
          <label style={{ fontSize: '0.875rem', minWidth: 120, color: '#374151' }}>
            vehicles.csv <span style={{ color: '#9ca3af' }}>(optional)</span>
          </label>
          <input ref={vehFileRef} type="file" accept=".csv" style={{ fontSize: '0.875rem' }} />
        </div>
        <div>
          <button
            onClick={() => void handlePreview()}
            disabled={isLoading}
            style={{ padding: '6px 14px', fontSize: '0.875rem', cursor: 'pointer', border: '1px solid #d1d5db', borderRadius: 6, background: '#f9fafb' }}
          >
            {isPreviewLoading ? 'Previewing…' : 'Preview'}
          </button>
        </div>
      </div>

      {phase.kind === 'error' && (
        <p style={{ color: '#b91c1c', fontSize: '0.875rem', marginBottom: '1rem' }}>
          {phase.message}
        </p>
      )}

      {phase.kind === 'preview' && (
        <>
          <h3 style={{ margin: '0 0 0.5rem', fontSize: '0.95rem', fontWeight: 700 }}>Employees</h3>
          <div style={{ display: 'flex', gap: '1.5rem', marginBottom: '0.75rem', fontSize: '0.875rem' }}>
            <span style={{ color: 'green' }}>Created: {phase.data.created}</span>
            <span style={{ color: '#1d4ed8' }}>Updated: {phase.data.updated}</span>
            <span style={{ color: '#6b7280' }}>Unchanged: {phase.data.unchanged}</span>
            <span style={{ color: '#b91c1c' }}>Rejected: {phase.data.rejected}</span>
          </div>
          <div style={{ overflowX: 'auto', marginBottom: '1.5rem' }}>
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

          {phase.data.vehicleRows.length > 0 && (
            <>
              <h3 style={{ margin: '0 0 0.5rem', fontSize: '0.95rem', fontWeight: 700 }}>Vehicles</h3>
              <div style={{ display: 'flex', gap: '1.5rem', marginBottom: '0.75rem', fontSize: '0.875rem' }}>
                <span style={{ color: 'green' }}>Valid: {phase.data.vehiclesValid}</span>
                <span style={{ color: '#b91c1c' }}>Rejected: {phase.data.vehiclesRejected}</span>
              </div>
              <div style={{ overflowX: 'auto', marginBottom: '1.5rem' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
                  <thead>
                    <tr style={{ background: '#f3f4f6' }}>
                      <th style={th}>Line</th>
                      <th style={th}>Subject</th>
                      <th style={th}>Plate</th>
                      <th style={th}>Status</th>
                      <th style={th}>Note</th>
                    </tr>
                  </thead>
                  <tbody>
                    {phase.data.vehicleRows.map(row => (
                      <tr key={row.lineNumber}>
                        <td style={td}>{row.lineNumber}</td>
                        <td style={td}>{row.externalSubject || '—'}</td>
                        <td style={td}>{row.licensePlate || '—'}</td>
                        <td style={{ ...td, color: VEHICLE_STATUS_COLOR[row.status] ?? '#111827', fontWeight: 600 }}>{row.status}</td>
                        <td style={td}>{row.reason ?? ''}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}

          {phase.data.rejected === 0 && phase.data.vehiclesRejected === 0 ? (
            <button
              onClick={() => void handleCommit()}
              disabled={isLoading}
              style={{ padding: '7px 18px', fontSize: '0.875rem', cursor: 'pointer', background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, fontWeight: 600 }}
            >
              {isCommitLoading
                ? 'Committing…'
                : `Commit ${phase.data.created + phase.data.updated} employee(s)${phase.data.vehiclesValid > 0 ? ` and ${phase.data.vehiclesValid} vehicle(s)` : ''}`}
            </button>
          ) : (
            <p style={{ fontSize: '0.875rem', color: '#92400e' }}>
              Fix {phase.data.rejected > 0 ? `${phase.data.rejected} rejected employee row(s)` : ''}
              {phase.data.rejected > 0 && phase.data.vehiclesRejected > 0 ? ' and ' : ''}
              {phase.data.vehiclesRejected > 0 ? `${phase.data.vehiclesRejected} rejected vehicle row(s)` : ''} before committing.
            </p>
          )}
        </>
      )}

      {phase.kind === 'committed' && (
        <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 8, padding: '1rem', fontSize: '0.875rem' }}>
          <strong style={{ color: '#15803d' }}>Import complete.</strong>{' '}
          {phase.data.applied} employee row(s) applied
          {phase.data.rejected > 0 && `, ${phase.data.rejected} rejected`}
          {phase.data.vehiclesApplied > 0 ? `, ${phase.data.vehiclesApplied} vehicle(s) applied` : ''}
          {phase.data.vehiclesRejected > 0 ? `, ${phase.data.vehiclesRejected} vehicle(s) rejected` : ''}.
          {(phase.data.errors.length > 0 || phase.data.vehicleErrors.length > 0) && (
            <ul style={{ marginTop: '0.5rem', paddingLeft: '1.25rem', color: '#b91c1c' }}>
              {phase.data.errors.map((e, i) => <li key={i}>{e}</li>)}
              {phase.data.vehicleErrors.map((e, i) => <li key={`v${i}`}>{e}</li>)}
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
  marginBottom: '1rem',
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
