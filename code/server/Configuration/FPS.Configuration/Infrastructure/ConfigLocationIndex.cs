using Dapr.Client;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Configuration.Infrastructure;

/// <summary>
/// Maintains a minimal per-tenant index of the location IDs that own location-scoped
/// configuration keys (slot lists, slot-change logs, per-location policy overrides).
/// Dapr key/value stores cannot enumerate by prefix, so these location-scoped keys have
/// no registry of their own. The index is appended idempotently whenever a location-scoped
/// key is first written, giving the destructive tenant purge a way to discover every location
/// to erase. Location IDs are stored canonically (lower-invariant) so the purge can rebuild
/// the exact keys the repositories wrote.
/// </summary>
internal static class ConfigLocationIndex
{
    internal const string ConfigStore = "configstore";

    internal static string Key(string tenantId)
        => TenantStorageKey.For("config-locations", tenantId, "all");

    internal static async Task AddAsync(
        DaprClient daprClient, string tenantId, string locationId, CancellationToken cancellationToken)
    {
        var canonical = locationId.ToLowerInvariant();
        var key = Key(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(ConfigStore, key, cancellationToken: cancellationToken) ?? [];
        if (index.Contains(canonical, StringComparer.Ordinal))
            return;

        index.Add(canonical);
        await daprClient.SaveStateAsync(ConfigStore, key, index, cancellationToken: cancellationToken);
    }

    internal static async Task<IReadOnlyList<string>> ReadAsync(
        DaprClient daprClient, string tenantId, CancellationToken cancellationToken)
        => await daprClient.GetStateAsync<List<string>>(ConfigStore, Key(tenantId), cancellationToken: cancellationToken) ?? [];
}
