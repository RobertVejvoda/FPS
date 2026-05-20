using System.Collections.Concurrent;
using FPS.Configuration.Domain;

namespace FPS.Configuration.Infrastructure;

public sealed class InMemorySlotChangeRepository : ISlotChangeRepository
{
    private readonly ConcurrentDictionary<(string, string), List<SlotChangeRecord>> _store = new();
    private readonly Lock _gate = new();

    public Task RecordAsync(SlotChangeRecord change, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var key = (change.TenantId, change.LocationId);
            if (!_store.TryGetValue(key, out var list))
            {
                list = [];
                _store[key] = list;
            }
            list.Add(change);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SlotChangeRecord>> GetHistoryAsync(
        string tenantId, string locationId, int limit = 20, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<SlotChangeRecord> result = _store.TryGetValue((tenantId, locationId), out var list)
                ? list.AsEnumerable().Reverse().Take(limit).ToList()
                : [];
            return Task.FromResult(result);
        }
    }
}
