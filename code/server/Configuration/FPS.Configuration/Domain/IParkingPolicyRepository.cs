namespace FPS.Configuration.Domain;

public interface IParkingPolicyRepository
{
    Task<ParkingPolicy?> GetTenantDefaultAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<ParkingPolicy?> GetLocationOverrideAsync(string tenantId, string locationId, CancellationToken cancellationToken = default);
    Task SaveAsync(ParkingPolicy policy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParkingPolicy>> GetHistoryAsync(string tenantId, string? locationId, int limit = 20, CancellationToken cancellationToken = default);
}
