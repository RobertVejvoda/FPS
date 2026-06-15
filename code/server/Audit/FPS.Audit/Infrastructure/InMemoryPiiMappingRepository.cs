using FPS.Audit.Domain;
using System.Collections.Concurrent;

namespace FPS.Audit.Infrastructure;

public sealed class InMemoryPiiMappingRepository : IPiiMappingRepository
{
    private readonly ConcurrentDictionary<(string tenantId, string userId), PiiMapping> store = new();

    public Task SaveAsync(PiiMapping mapping, CancellationToken cancellationToken = default)
    {
        store[(mapping.TenantId, mapping.UserId)] = mapping;
        return Task.CompletedTask;
    }

    public Task DeleteByUserIdAsync(string userId, string tenantId, CancellationToken cancellationToken = default)
    {
        store.TryRemove((tenantId, userId), out _);
        return Task.CompletedTask;
    }

    public Task DeleteByActorHashAsync(string actorHash, string tenantId, CancellationToken cancellationToken = default)
    {
        var keys = store
            .Where(kv => kv.Key.tenantId == tenantId && kv.Value.ActorHash == actorHash)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in keys)
            store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string userId, string tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.ContainsKey((tenantId, userId)));

    public Task<IReadOnlyDictionary<string, PiiMapping>> GetByActorHashesAsync(
        string tenantId, IReadOnlyList<string> actorHashes, CancellationToken cancellationToken = default)
    {
        if (actorHashes.Count == 0)
            return Task.FromResult<IReadOnlyDictionary<string, PiiMapping>>(
                new Dictionary<string, PiiMapping>(StringComparer.OrdinalIgnoreCase));

        var wanted = new HashSet<string>(actorHashes, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, PiiMapping>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, mapping) in store)
        {
            if (!string.Equals(key.tenantId, tenantId, StringComparison.Ordinal)) continue;
            if (wanted.Contains(mapping.ActorHash))
                result[mapping.ActorHash] = mapping;
        }
        return Task.FromResult<IReadOnlyDictionary<string, PiiMapping>>(result);
    }
}
