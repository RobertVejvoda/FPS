using Dapr.Client;
using FPS.Configuration.Domain;

namespace FPS.Configuration.Infrastructure;

/// <summary>
/// Destructive per-tenant purge of Configuration state. Rebuilds the exact keys the repositories
/// wrote — reusing their key builders as the single source of truth so the purge never guesses a
/// key shape — and deletes each one that is present. Location-scoped keys are found through the
/// <see cref="ConfigLocationIndex"/> because Dapr key/value stores cannot enumerate by prefix.
/// </summary>
public sealed class ConfigurationTenantPurger(DaprClient daprClient) : IConfigurationTenantPurger
{
    private const string ConfigStore = "configstore";

    public async Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var removed = 0;

        // Tenant-default policy — the only Configuration key that is not location-scoped.
        removed += await DeleteIfPresentAsync<List<ParkingPolicy>>(
            DaprParkingPolicyRepository.TenantDefaultKey(tenantId), cancellationToken);

        var locations = await ConfigLocationIndex.ReadAsync(daprClient, tenantId, cancellationToken);
        foreach (var locationId in locations)
        {
            removed += await DeleteIfPresentAsync<List<ParkingPolicy>>(
                DaprParkingPolicyRepository.LocationOverrideKey(tenantId, locationId), cancellationToken);
            removed += await DeleteIfPresentAsync<List<ParkingSlot>>(
                DaprParkingSlotRepository.SlotListKey(tenantId, locationId), cancellationToken);
            removed += await DeleteIfPresentAsync<List<SlotChangeRecord>>(
                DaprSlotChangeRepository.SlotChangeKey(tenantId, locationId), cancellationToken);
            removed += await DeleteIfPresentAsync<SeatMap>(
                DaprSeatMapRepository.SeatMapKey(tenantId, locationId), cancellationToken);
            removed += await DeleteIfPresentAsync<List<SeatBlock>>(
                DaprSeatBlockRepository.SeatBlockKey(tenantId, locationId), cancellationToken);
            removed += await DeleteIfPresentAsync<List<SeatMapChangeRecord>>(
                DaprSeatMapChangeRepository.SeatChangeKey(tenantId, locationId), cancellationToken);
        }

        // Drop the index last so a re-purge is a clean no-op.
        if (locations.Count > 0)
            await daprClient.DeleteStateAsync(ConfigStore, ConfigLocationIndex.Key(tenantId), cancellationToken: cancellationToken);

        return removed;
    }

    private async Task<int> DeleteIfPresentAsync<T>(string key, CancellationToken cancellationToken)
    {
        var existing = await daprClient.GetStateAsync<T>(ConfigStore, key, cancellationToken: cancellationToken);
        if (existing is null)
            return 0;

        await daprClient.DeleteStateAsync(ConfigStore, key, cancellationToken: cancellationToken);
        return 1;
    }
}
