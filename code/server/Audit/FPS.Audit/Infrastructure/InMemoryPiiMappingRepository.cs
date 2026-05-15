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
}
