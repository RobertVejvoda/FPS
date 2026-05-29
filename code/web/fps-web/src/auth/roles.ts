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

// Returns the first route this user can access, for default redirects.
export function defaultRoute(roles: string[]): string {
  if (canAccessBookings(roles)) return '/bookings';
  if (canAccessHrOperations(roles)) return '/hr-operations';
  if (canAccessReporting(roles)) return '/reporting';
  if (canAccessAudit(roles)) return '/audit';
  if (canAccessTenantAdmin(roles)) return '/tenant-admin';
  return '/profile';
}
