using Dapr.Client;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Configuration.Infrastructure;

public sealed class DaprParkingPolicyRepository : IParkingPolicyRepository
{
    private readonly DaprClient daprClient;
    private const string ConfigStore = "configstore";

    // Policy version lists are stored at a single key per (tenant, scope).
    // The scope is "default" for tenant-level policies or the locationId for overrides.
    // Max versions retained per scope — protects key size; policy changes are infrequent.
    private const int MaxVersionsRetained = 50;

    public DaprParkingPolicyRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<ParkingPolicy?> GetTenantDefaultAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var list = await LoadVersionsAsync(tenantId, scope: "default", cancellationToken);
        return list.Count > 0 ? list[^1] : null;
    }

    public async Task<ParkingPolicy?> GetLocationOverrideAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        var list = await LoadVersionsAsync(tenantId, LocationScope(locationId), cancellationToken);
        return list.Count > 0 ? list[^1] : null;
    }

    public async Task SaveAsync(ParkingPolicy policy, CancellationToken cancellationToken = default)
    {
        var scope = policy.LocationId is not null ? LocationScope(policy.LocationId) : "default";
        var key = PolicyKey(policy.TenantId, scope);
        var list = await LoadVersionsAsync(policy.TenantId, scope, cancellationToken);
        list.Add(policy);
        if (list.Count > MaxVersionsRetained)
            list.RemoveRange(0, list.Count - MaxVersionsRetained);
        await daprClient.SaveStateAsync(ConfigStore, key, list, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ParkingPolicy>> GetHistoryAsync(
        string tenantId, string? locationId, int limit = 20, CancellationToken cancellationToken = default)
    {
        var scope = locationId is not null ? LocationScope(locationId) : "default";
        var list = await LoadVersionsAsync(tenantId, scope, cancellationToken);
        return list.AsEnumerable().Reverse().Take(limit).ToList();
    }

    private async Task<List<ParkingPolicy>> LoadVersionsAsync(string tenantId, string scope, CancellationToken cancellationToken)
        => await daprClient.GetStateAsync<List<ParkingPolicy>>(
               ConfigStore, PolicyKey(tenantId, scope), cancellationToken: cancellationToken) ?? [];

    private static string PolicyKey(string tenantId, string scope)
        => TenantStorageKey.For("config-policy", tenantId, scope);

    private static string LocationScope(string locationId)
        => locationId.ToLowerInvariant();
}
