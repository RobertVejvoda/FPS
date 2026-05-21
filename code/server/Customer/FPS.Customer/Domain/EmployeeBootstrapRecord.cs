namespace FPS.Customer.Domain;

// Minimal employee/profile facts required for pilot/live tenant use.
// Stores only what FPS needs for Booking policy evaluation, notification, and support.
// Raw subjects are never stored — only pseudonymised hashes.
public sealed class EmployeeBootstrapRecord
{
    public string TenantId { get; init; } = string.Empty;
    // SHA-256 of the stable external OIDC subject or local-account marker.
    public string ExternalSubjectHash { get; init; } = string.Empty;
    // Optional employee/staff identifier — only when policy or support requires it.
    public string? EmployeeId { get; init; }
    public bool IsActive { get; set; }
    // Tenant-scoped FPS roles (employee, hr_manager, admin, report_viewer).
    public IReadOnlyList<string> FpsRoles { get; set; } = [];
    // Operational notification address (email). Omit when not needed.
    public string? NotificationAddress { get; set; }
    public string? HomeLocationId { get; set; }
    // Policy-dependent eligibility facts.
    public bool ParkingEligible { get; set; }
    public bool HasCompanyCar { get; set; }
    public bool AccessibilityEligible { get; set; }
    public bool ReservedSpaceEligible { get; set; }
    // "sso-bootstrap" | "admin-entry" | "file-import"
    public string FactSource { get; init; } = string.Empty;
    public string RecordedByHash { get; init; } = string.Empty;
    public DateTimeOffset RecordedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
