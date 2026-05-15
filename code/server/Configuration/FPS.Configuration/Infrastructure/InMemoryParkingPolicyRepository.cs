using FPS.Configuration.Domain;

namespace FPS.Configuration.Infrastructure;

public sealed class InMemoryParkingPolicyRepository : IParkingPolicyRepository
{
    private readonly Dictionary<(string tenantId, string? locationId), ParkingPolicy> store = new();
    private readonly Lock gate = new();

    public Task<ParkingPolicy?> GetTenantDefaultAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            store.TryGetValue((tenantId, null), out var policy);
            return Task.FromResult(policy);
        }
    }

    public Task<ParkingPolicy?> GetLocationOverrideAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            store.TryGetValue((tenantId, locationId), out var policy);
            return Task.FromResult(policy);
        }
    }

    public Task SaveAsync(ParkingPolicy policy, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            store[(policy.TenantId, policy.LocationId)] = policy;
        }
        return Task.CompletedTask;
    }
}
