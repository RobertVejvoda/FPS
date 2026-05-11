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
    public string SnapshotVersion { get; init; } = string.Empty;

    public bool IsActive => Status == ProfileStatus.Active;
    public IReadOnlyList<Vehicle> ActiveVehicles => Vehicles.Where(v => v.IsActive).ToList();
}

public enum ProfileStatus { Active, Inactive, Suspended }
