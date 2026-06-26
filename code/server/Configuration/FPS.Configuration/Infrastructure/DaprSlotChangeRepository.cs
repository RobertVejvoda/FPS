using Dapr.Client;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Configuration.Infrastructure;

public sealed class DaprSlotChangeRepository : ISlotChangeRepository
{
    private readonly DaprClient daprClient;
    private const string ConfigStore = "configstore";
    private const int MaxChangesRetained = 100;

    public DaprSlotChangeRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task RecordAsync(SlotChangeRecord change, CancellationToken cancellationToken = default)
    {
        var key = SlotChangeKey(change.TenantId, change.LocationId);
        var list = await LoadChangesAsync(key, cancellationToken);
        list.Add(change);
        if (list.Count > MaxChangesRetained)
            list.RemoveRange(0, list.Count - MaxChangesRetained);
        await daprClient.SaveStateAsync(ConfigStore, key, list, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<SlotChangeRecord>> GetHistoryAsync(
        string tenantId, string locationId, int limit = 20, CancellationToken cancellationToken = default)
    {
        var list = await LoadChangesAsync(SlotChangeKey(tenantId, locationId), cancellationToken);
        return list.AsEnumerable().Reverse().Take(limit).ToList();
    }

    private async Task<List<SlotChangeRecord>> LoadChangesAsync(string key, CancellationToken cancellationToken)
        => await daprClient.GetStateAsync<List<SlotChangeRecord>>(ConfigStore, key, cancellationToken: cancellationToken) ?? [];

    private static string SlotChangeKey(string tenantId, string locationId)
        => TenantStorageKey.For("config-slotchange", tenantId, locationId.ToLowerInvariant());
}
