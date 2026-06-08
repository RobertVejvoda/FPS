export const FpsRole = {
  Employee: 'employee',
  HrManager: 'hr_manager',
  Admin: 'admin',
  ReportViewer: 'report_viewer',
  Auditor: 'auditor',
} as const;

export type FpsRole = (typeof FpsRole)[keyof typeof FpsRole];

export function hasRole(roles: string[], ...required: FpsRole[]): boolean {
  return required.some(r => roles.some(ur => ur.toLowerCase() === r));
}

// Employee self-service surfaces — require the employee role explicitly.
export function canAccessBookings(roles: string[]): boolean {
  return hasRole(roles, FpsRole.Employee);
}

export function canAccessProfile(roles: string[]): boolean {
  return hasRole(roles, FpsRole.Employee);
}

export function canAccessNotifications(roles: string[]): boolean {
  return hasRole(roles, FpsRole.Employee);
}

// Reporting surfaces.
export function canAccessReporting(roles: string[]): boolean {
  return hasRole(roles, FpsRole.HrManager, FpsRole.Admin, FpsRole.ReportViewer);
}

// Configuration surfaces.
export function canAccessConfiguration(roles: string[]): boolean {
  return hasRole(roles, FpsRole.HrManager, FpsRole.Admin);
}

// HR operations workspace — Draw controls and request cancellation.
export function canAccessHrOperations(roles: string[]): boolean {
  return hasRole(roles, FpsRole.HrManager, FpsRole.Admin);
}

// Audit surfaces.
export function canAccessAudit(roles: string[]): boolean {
  return hasRole(roles, FpsRole.Auditor, FpsRole.Admin);
}

// Tenant admin surfaces.
export function canAccessTenantAdmin(roles: string[]): boolean {
  return hasRole(roles, FpsRole.Admin);
}

// Simulation clock controls — advance/reset require hr_manager or admin.
export function canControlSimulation(roles: string[]): boolean {
  return hasRole(roles, FpsRole.HrManager, FpsRole.Admin);
}

const ROLE_LABELS: Record<string, string> = {
  employee: 'Employee',
  hr_manager: 'HR Manager',
  admin: 'Administrator',
  report_viewer: 'Report Viewer',
  auditor: 'Auditor',
};

export function formatRoles(roles: string[]): string {
  const seen = new Set<string>();
  const labels: string[] = [];
  for (const r of roles) {
    const label = ROLE_LABELS[r.toLowerCase()] ?? null;
    if (label && !seen.has(label)) { seen.add(label); labels.push(label); }
  }
  return labels.join(', ') || 'Employee';
}

// Returns the first route this user can access, for default redirects.
// Priority: employee → admin → hr_manager → reporting → audit → profile.
// Admin is checked before hr-operations because canAccessHrOperations also matches admin.
export function defaultRoute(roles: string[]): string {
  if (canAccessBookings(roles)) return '/bookings';
  if (canAccessTenantAdmin(roles)) return '/tenant-admin';
  if (canAccessHrOperations(roles)) return '/hr-operations';
  if (canAccessReporting(roles)) return '/reporting';
  if (canAccessAudit(roles)) return '/auditor-workspace';
  return '/profile';
}
