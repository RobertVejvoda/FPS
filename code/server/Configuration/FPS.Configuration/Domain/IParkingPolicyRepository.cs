namespace FPS.Configuration.Domain;

public interface IParkingPolicyRepository
{
    Task<ParkingPolicy?> GetTenantDefaultAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<ParkingPolicy?> GetLocationOverrideAsync(string tenantId, string locationId, CancellationToken cancellationToken = default);
    Task SaveAsync(ParkingPolicy policy, CancellationToken cancellationToken = default);
}
