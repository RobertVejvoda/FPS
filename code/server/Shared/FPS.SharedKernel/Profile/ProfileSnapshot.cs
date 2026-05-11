namespace FPS.SharedKernel.Profile;

public sealed record ProfileSnapshot(
    string TenantId,
    string UserId,
    string ProfileStatus,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible,
    IReadOnlyList<VehicleSnapshot> Vehicles,
    string SnapshotVersion);

public sealed record VehicleSnapshot(
    string VehicleId,
    string LicensePlate,
    string VehicleType,
    bool IsElectric,
    bool IsActive);

public interface IProfileSnapshotService
{
    Task<ProfileSnapshot?> GetSnapshotAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
}
