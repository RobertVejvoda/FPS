using Dapr.Client;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Configuration.Infrastructure;

public sealed class DaprParkingPolicyRepository : IParkingPolicyRepository
{
    private readonly DaprClient daprClient;
    private const string ConfigStore = "configstore";

    // Tenant-default and location-override policies use structurally distinct key prefixes
    // so that a location named "default" (or any other value) cannot collide with the
    // tenant-level policy key.
    //   Tenant default:      config-policy:{tenantId}:tenant-default
    //   Location override:   config-policy-location:{tenantId}:{locationId}
    private const int MaxVersionsRetained = 50;

    public DaprParkingPolicyRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<ParkingPolicy?> GetTenantDefaultAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var list = await LoadVersionsAsync(TenantDefaultKey(tenantId), cancellationToken);
        return list.Count > 0 ? list[^1] : null;
    }

    public async Task<ParkingPolicy?> GetLocationOverrideAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        var list = await LoadVersionsAsync(LocationOverrideKey(tenantId, locationId), cancellationToken);
        return list.Count > 0 ? list[^1] : null;
    }

    public async Task SaveAsync(ParkingPolicy policy, CancellationToken cancellationToken = default)
    {
        var key = policy.LocationId is not null
            ? LocationOverrideKey(policy.TenantId, policy.LocationId)
            : TenantDefaultKey(policy.TenantId);
        var list = await LoadVersionsAsync(key, cancellationToken);
        list.Add(policy);
        if (list.Count > MaxVersionsRetained)
            list.RemoveRange(0, list.Count - MaxVersionsRetained);
        await daprClient.SaveStateAsync(ConfigStore, key, list, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ParkingPolicy>> GetHistoryAsync(
        string tenantId, string? locationId, int limit = 20, CancellationToken cancellationToken = default)
    {
        var key = locationId is not null
            ? LocationOverrideKey(tenantId, locationId)
            : TenantDefaultKey(tenantId);
        var list = await LoadVersionsAsync(key, cancellationToken);
        return list.AsEnumerable().Reverse().Take(limit).ToList();
    }

    private async Task<List<ParkingPolicy>> LoadVersionsAsync(string key, CancellationToken cancellationToken)
        => await daprClient.GetStateAsync<List<ParkingPolicy>>(ConfigStore, key, cancellationToken: cancellationToken) ?? [];

    private static string TenantDefaultKey(string tenantId)
        => TenantStorageKey.For("config-policy", tenantId, "tenant-default");

    private static string LocationOverrideKey(string tenantId, string locationId)
        => TenantStorageKey.For("config-policy-location", tenantId, locationId.ToLowerInvariant());
}
