using Dapr.Client;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Configuration.Infrastructure;

public sealed class DaprSeatMapRepository : ISeatMapRepository
{
    private readonly DaprClient daprClient;
    private const string ConfigStore = "configstore";

    public DaprSeatMapRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<SeatMap> GetByLocationAsync(
        string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        return await daprClient.GetStateAsync<SeatMap>(
                   ConfigStore, SeatMapKey(tenantId, locationId), cancellationToken: cancellationToken)
               ?? new SeatMap();
    }

    public async Task ReplaceLocationSeatMapAsync(
        string tenantId, string locationId, SeatMap map, CancellationToken cancellationToken = default)
    {
        await daprClient.SaveStateAsync(
            ConfigStore, SeatMapKey(tenantId, locationId), map, cancellationToken: cancellationToken);

        // Writing a location's seat map is a first-write of a location-scoped key: record the
        // location in the per-tenant index the destructive purge uses to discover these keys.
        await ConfigLocationIndex.AddAsync(daprClient, tenantId, locationId, cancellationToken);
    }

    // The whole grid (areas + seats) for a location is stored as one document at
    // config-seatmap:{tenantId}:{locationId}. ReplaceLocationSeatMapAsync atomically
    // overwrites the full map in one write, mirroring the parking slot list.
    internal static string SeatMapKey(string tenantId, string locationId)
        => TenantStorageKey.For("config-seatmap", tenantId, locationId.ToLowerInvariant());
}

public sealed class DaprSeatBlockRepository : ISeatBlockRepository
{
    private readonly DaprClient daprClient;
    private const string ConfigStore = "configstore";

    public DaprSeatBlockRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<IReadOnlyList<SeatBlock>> GetByLocationAsync(
        string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        return await daprClient.GetStateAsync<List<SeatBlock>>(
                   ConfigStore, SeatBlockKey(tenantId, locationId), cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task AddAsync(SeatBlock block, CancellationToken cancellationToken = default)
    {
        var key = SeatBlockKey(block.TenantId, block.LocationId);
        var list = await daprClient.GetStateAsync<List<SeatBlock>>(
                       ConfigStore, key, cancellationToken: cancellationToken) ?? [];
        list.Add(block);
        await daprClient.SaveStateAsync(ConfigStore, key, list, cancellationToken: cancellationToken);
        await ConfigLocationIndex.AddAsync(daprClient, block.TenantId, block.LocationId, cancellationToken);
    }

    public async Task<bool> RemoveAsync(
        string tenantId, string locationId, string blockId, CancellationToken cancellationToken = default)
    {
        var key = SeatBlockKey(tenantId, locationId);
        var list = await daprClient.GetStateAsync<List<SeatBlock>>(
                       ConfigStore, key, cancellationToken: cancellationToken) ?? [];
        var removed = list.RemoveAll(b => b.BlockId == blockId);
        if (removed == 0) return false;
        await daprClient.SaveStateAsync(ConfigStore, key, list, cancellationToken: cancellationToken);
        return true;
    }

    internal static string SeatBlockKey(string tenantId, string locationId)
        => TenantStorageKey.For("config-seatblocks", tenantId, locationId.ToLowerInvariant());
}

public sealed class DaprSeatMapChangeRepository : ISeatMapChangeRepository
{
    private readonly DaprClient daprClient;
    private const string ConfigStore = "configstore";
    private const int MaxChangesRetained = 100;

    public DaprSeatMapChangeRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task RecordAsync(SeatMapChangeRecord change, CancellationToken cancellationToken = default)
    {
        var key = SeatChangeKey(change.TenantId, change.LocationId);
        var list = await daprClient.GetStateAsync<List<SeatMapChangeRecord>>(
                       ConfigStore, key, cancellationToken: cancellationToken) ?? [];
        list.Add(change);
        if (list.Count > MaxChangesRetained)
            list.RemoveRange(0, list.Count - MaxChangesRetained);
        await daprClient.SaveStateAsync(ConfigStore, key, list, cancellationToken: cancellationToken);
        await ConfigLocationIndex.AddAsync(daprClient, change.TenantId, change.LocationId, cancellationToken);
    }

    public async Task<IReadOnlyList<SeatMapChangeRecord>> GetHistoryAsync(
        string tenantId, string locationId, int limit, CancellationToken cancellationToken = default)
    {
        var list = await daprClient.GetStateAsync<List<SeatMapChangeRecord>>(
                       ConfigStore, SeatChangeKey(tenantId, locationId), cancellationToken: cancellationToken) ?? [];
        return list.OrderByDescending(c => c.ChangedAt).Take(limit).ToList();
    }

    internal static string SeatChangeKey(string tenantId, string locationId)
        => TenantStorageKey.For("config-seatchange", tenantId, locationId.ToLowerInvariant());
}
