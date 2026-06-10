namespace FPS.Profile.Domain;

public sealed class UserProfile
{
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public ProfileStatus Status { get; init; }
    public bool ParkingEligible { get; init; }
    public bool HasCompanyCar { get; init; }
    public bool AccessibilityEligible { get; init; }
    public bool ReservedSpaceEligible { get; init; }
    public IReadOnlyList<Vehicle> Vehicles { get; init; } = [];
    // Optional staff/employee identifier — only stored when policy or support requires it.
    public string? EmployeeId { get; init; }
    // Optional display name from HR import or admin entry — HR/admin surfaces only.
    public string? DisplayName { get; init; }
    // Tenant-scoped FPS roles for this user (employee, hr_manager, admin, report_viewer).
    public IReadOnlyList<string> FpsRoles { get; init; } = [];
    // Operational notification email — omit when not needed.
    public string? NotificationAddress { get; init; }
    public string? HomeLocationId { get; init; }
    public string SnapshotVersion { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    // Source of the profile facts: "sso-claims", "admin-seed", "admin-entry", or "import".
    public string FactSource { get; init; } = string.Empty;

    public bool IsActive => Status == ProfileStatus.Active;
    public IReadOnlyList<Vehicle> ActiveVehicles => Vehicles.Where(v => v.IsActive).ToList();
}

public enum ProfileStatus { Active, Inactive, Suspended }
