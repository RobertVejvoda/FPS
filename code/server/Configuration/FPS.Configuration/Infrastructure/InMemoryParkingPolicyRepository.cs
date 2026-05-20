using FPS.Configuration.Domain;

namespace FPS.Configuration.Infrastructure;

public sealed class InMemoryParkingPolicyRepository : IParkingPolicyRepository
{
    private readonly Dictionary<(string tenantId, string? locationId), List<ParkingPolicy>> history = new();
    private readonly Lock gate = new();

    public Task<ParkingPolicy?> GetTenantDefaultAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var versions = GetVersions((tenantId, null));
            return Task.FromResult(versions.Count > 0 ? versions[^1] : null);
        }
    }

    public Task<ParkingPolicy?> GetLocationOverrideAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var versions = GetVersions((tenantId, locationId));
            return Task.FromResult(versions.Count > 0 ? versions[^1] : null);
        }
    }

    public Task SaveAsync(ParkingPolicy policy, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            GetVersions((policy.TenantId, policy.LocationId)).Add(policy);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ParkingPolicy>> GetHistoryAsync(
        string tenantId, string? locationId, int limit = 20, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var versions = GetVersions((tenantId, locationId));
            IReadOnlyList<ParkingPolicy> result = versions
                .AsEnumerable()
                .Reverse()
                .Take(limit)
                .ToList();
            return Task.FromResult(result);
        }
    }

    private List<ParkingPolicy> GetVersions((string, string?) key)
    {
        if (!history.TryGetValue(key, out var list))
        {
            list = [];
            history[key] = list;
        }
        return list;
    }
}
