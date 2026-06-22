using System.Collections.Concurrent;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class InMemoryTenantRepository : ITenantRepository
{
    private readonly ConcurrentDictionary<string, TenantWorkspace> byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> slugToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> domainToId = new(StringComparer.OrdinalIgnoreCase);

    public Task<TenantWorkspace?> GetAsync(string tenantId, CancellationToken ct) =>
        Task.FromResult(byId.TryGetValue(tenantId, out var t) ? t : null);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        Task.FromResult(slugToId.ContainsKey(slug));

    public Task<TenantWorkspace?> FindByDiscoveryDomainAsync(string domain, CancellationToken ct)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        if (!domainToId.TryGetValue(normalized, out var tenantId)) return Task.FromResult<TenantWorkspace?>(null);
        return GetAsync(tenantId, ct);
    }

    public Task<bool> IsDomainRegisteredAsync(string domain, string? excludeTenantId, CancellationToken ct)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        if (!domainToId.TryGetValue(normalized, out var tenantId)) return Task.FromResult(false);
        if (excludeTenantId is not null && tenantId.Equals(excludeTenantId, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);
        return Task.FromResult(true);
    }

    public Task SaveAsync(TenantWorkspace tenant, CancellationToken ct)
    {
        byId[tenant.TenantId] = tenant;
        slugToId[tenant.Slug] = tenant.TenantId;

        // Rebuild domain index: remove stale entries, add current ones.
        var currentDomains = tenant.DiscoveryDomains.Select(d => d.Domain).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in domainToId.Keys.ToList())
        {
            if (domainToId.TryGetValue(key, out var tid) &&
                tid.Equals(tenant.TenantId, StringComparison.OrdinalIgnoreCase) &&
                !currentDomains.Contains(key))
            {
                domainToId.TryRemove(key, out _);
            }
        }
        foreach (var domain in tenant.DiscoveryDomains)
            domainToId[domain.Domain] = tenant.TenantId;

        return Task.CompletedTask;
    }
}
