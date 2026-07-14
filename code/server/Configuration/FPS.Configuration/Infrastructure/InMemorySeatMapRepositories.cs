using System.Collections.Concurrent;
using FPS.Configuration.Domain;

namespace FPS.Configuration.Infrastructure;

public sealed class InMemorySeatMapRepository : ISeatMapRepository
{
    private readonly ConcurrentDictionary<string, SeatMap> maps = new(StringComparer.OrdinalIgnoreCase);

    public Task<SeatMap> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
        => Task.FromResult(maps.TryGetValue(Key(tenantId, locationId), out var map) ? map : new SeatMap());

    public Task ReplaceLocationSeatMapAsync(string tenantId, string locationId, SeatMap map, CancellationToken cancellationToken = default)
    {
        maps[Key(tenantId, locationId)] = map;
        return Task.CompletedTask;
    }

    private static string Key(string tenantId, string locationId) => $"{tenantId}:{locationId.ToLowerInvariant()}";
}

public sealed class InMemorySeatBlockRepository : ISeatBlockRepository
{
    private readonly ConcurrentDictionary<string, List<SeatBlock>> blocks = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<SeatBlock>> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        var list = blocks.TryGetValue(Key(tenantId, locationId), out var b) ? b : [];
        return Task.FromResult<IReadOnlyList<SeatBlock>>(list.ToList());
    }

    public Task AddAsync(SeatBlock block, CancellationToken cancellationToken = default)
    {
        var list = blocks.GetOrAdd(Key(block.TenantId, block.LocationId), _ => []);
        lock (list) { list.Add(block); }
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string tenantId, string locationId, string blockId, CancellationToken cancellationToken = default)
    {
        if (!blocks.TryGetValue(Key(tenantId, locationId), out var list)) return Task.FromResult(false);
        lock (list) { return Task.FromResult(list.RemoveAll(b => b.BlockId == blockId) > 0); }
    }

    private static string Key(string tenantId, string locationId) => $"{tenantId}:{locationId.ToLowerInvariant()}";
}

public sealed class InMemorySeatMapChangeRepository : ISeatMapChangeRepository
{
    private readonly ConcurrentDictionary<string, List<SeatMapChangeRecord>> changes = new(StringComparer.OrdinalIgnoreCase);

    public Task RecordAsync(SeatMapChangeRecord change, CancellationToken cancellationToken = default)
    {
        var list = changes.GetOrAdd($"{change.TenantId}:{change.LocationId.ToLowerInvariant()}", _ => []);
        lock (list) { list.Add(change); }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SeatMapChangeRecord>> GetHistoryAsync(string tenantId, string locationId, int limit, CancellationToken cancellationToken = default)
    {
        var list = changes.TryGetValue($"{tenantId}:{locationId.ToLowerInvariant()}", out var c) ? c : [];
        return Task.FromResult<IReadOnlyList<SeatMapChangeRecord>>(
            list.OrderByDescending(x => x.ChangedAt).Take(limit).ToList());
    }
}
