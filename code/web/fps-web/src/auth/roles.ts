export const FpsRole = {
  Employee: 'employee',
  HrManager: 'hr_manager',
  Admin: 'admin',
  ReportViewer: 'report_viewer',
  Auditor: 'auditor',
  // Platform plane (PLAT001) — cross-tenant FairSpot operator roles. The backend only
  // ever mints these on a token from the trusted platform issuer; a tenant token can
  // never carry one. Their presence in the roles array is what marks the platform plane.
  PlatformAdmin: 'platform_admin',
  PlatformOperator: 'platform_operator',
  PlatformAuditor: 'platform_auditor',
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

// HR managers and admins can now receive HR-targeted notification variants
// (NOTIF #478: booking.requestSubmitted.hr, .requestCancelled.hr, .drawCompleted.hr).
// Without HR/admin here the banner can show, but the nav item disappears
// and the /notifications route 403s — issue #478 Codex review on PR #487.
export function canAccessNotifications(roles: string[]): boolean {
  return hasRole(roles, FpsRole.Employee, FpsRole.HrManager, FpsRole.Admin);
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

// Parking Map — operational capacity view restricted to HR/admin (issue #483).
// Employees moved to their personal assignment history on BookingHistoryPage;
// the general site map carries no employee-relevant information. Auditor /
// report_viewer also lose access because their workflows live elsewhere
// (auditor workspace, reports) — the map's value is purely operational.
export function canAccessParkingMap(roles: string[]): boolean {
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

// ── Platform plane (PLAT008A) ───────────────────────────────────────────────
// The console is reachable only by a platform-plane identity. The web app cannot see the
// token issuer; it infers the plane purely from the presence of a platform_* role (the
// backend has already gated issuer → roles). Any platform_* role => platform plane.
export function isPlatformPlane(roles: string[]): boolean {
  return hasRole(roles, FpsRole.PlatformAdmin, FpsRole.PlatformOperator, FpsRole.PlatformAuditor);
}

// Route guard for the operator console surface. Alias of isPlatformPlane — a tenant/customer
// token (any tenant role, even admin) is not a platform identity and is denied.
export function canAccessPlatformConsole(roles: string[]): boolean {
  return isPlatformPlane(roles);
}

// Only platform_admin sees real $ cost (the locked rule that cost stays platform-internal).
export function isPlatformAdmin(roles: string[]): boolean {
  return hasRole(roles, FpsRole.PlatformAdmin);
}

// Onboarding triage (approve/reject) is admin/operator; platform_auditor is read-only.
export function canTriagePlatformOnboarding(roles: string[]): boolean {
  return hasRole(roles, FpsRole.PlatformAdmin, FpsRole.PlatformOperator);
}

const ROLE_LABELS: Record<string, string> = {
  employee: 'Employee',
  hr_manager: 'HR Manager',
  admin: 'Administrator',
  report_viewer: 'Report Viewer',
  auditor: 'Auditor',
  platform_admin: 'Platform Admin',
  platform_operator: 'Platform Operator',
  platform_auditor: 'Platform Auditor',
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
// Priority: platform → employee → admin → hr_manager → reporting → audit → profile.
// A platform identity has no tenant surfaces, so it always lands in the operator console.
// Admin is checked before hr-operations because canAccessHrOperations also matches admin.
export function defaultRoute(roles: string[]): string {
  if (isPlatformPlane(roles)) return '/platform/overview';
  if (canAccessBookings(roles)) return '/bookings';
  if (canAccessTenantAdmin(roles)) return '/tenant-admin';
  if (canAccessHrOperations(roles)) return '/hr-operations';
  if (canAccessReporting(roles)) return '/reporting';
  if (canAccessAudit(roles)) return '/auditor-workspace';
  return '/profile';
}

// ── Tenant modules (PLAT007B) ───────────────────────────────────────────────
// A tenant's product modules. Parking is the default and, today, the only implemented module;
// Seats is the contract the seats slice (#710) builds on.
export type TenantModule = 'Parking' | 'Seats';

// Landing route for one module's primary experience (for a tenant user). Parking is the whole
// tenant app today, so it maps to the role-based default; Seats gets its own surface with #710.
export function moduleLandingRoute(primaryModule: string, roles: string[]): string {
  if (primaryModule === 'Seats') return '/seats';
  return defaultRoute(roles);
}

// Default landing for a tenant user that honours the primary module — but ONLY when more than one
// module is enabled. Per PLAT007B a single-module tenant shows no module selector and behaves
// exactly as before, so a Parking-only tenant (including Green Logistics) always gets the plain
// role-based defaultRoute. This is the routing contract #710 wires once module data reaches the
// session and a Seats surface exists.
export function tenantLandingRoute(roles: string[], primaryModule: string, enabledModules: string[]): string {
  return enabledModules.length > 1 ? moduleLandingRoute(primaryModule, roles) : defaultRoute(roles);
}
