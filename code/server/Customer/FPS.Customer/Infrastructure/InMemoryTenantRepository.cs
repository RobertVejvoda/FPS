using System.Collections.Concurrent;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class InMemoryTenantRepository : ITenantRepository
{
    private readonly ConcurrentDictionary<string, TenantWorkspace> byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> slugToId = new(StringComparer.OrdinalIgnoreCase);

    public Task<TenantWorkspace?> GetAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(byId.TryGetValue(tenantId, out var t) ? t : null);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        Task.FromResult(slugToId.ContainsKey(slug));

    public Task SaveAsync(TenantWorkspace tenant, CancellationToken ct)
    {
        byId[tenant.TenantId] = tenant;
        slugToId[tenant.Slug] = tenant.TenantId;
        return Task.CompletedTask;
    }
}
