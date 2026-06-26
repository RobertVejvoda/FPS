using Dapr.Client;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Configuration.Infrastructure;

public sealed class DaprParkingSlotRepository : IParkingSlotRepository
{
    private readonly DaprClient daprClient;
    private const string ConfigStore = "configstore";

    public DaprParkingSlotRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<IReadOnlyList<ParkingSlot>> GetByLocationAsync(
        string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        return await daprClient.GetStateAsync<List<ParkingSlot>>(
                   ConfigStore, SlotListKey(tenantId, locationId), cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task ReplaceLocationSlotsAsync(
        string tenantId, string locationId, IReadOnlyList<ParkingSlot> slots, CancellationToken cancellationToken = default)
    {
        await daprClient.SaveStateAsync(
            ConfigStore, SlotListKey(tenantId, locationId), slots.ToList(), cancellationToken: cancellationToken);
    }

    // Slots for a location are stored as a single list at config-slots:{tenantId}:{locationId}.
    // ReplaceLocationSlotsAsync atomically overwrites all slots in one write.
    private static string SlotListKey(string tenantId, string locationId)
        => TenantStorageKey.For("config-slots", tenantId, locationId.ToLowerInvariant());
}
