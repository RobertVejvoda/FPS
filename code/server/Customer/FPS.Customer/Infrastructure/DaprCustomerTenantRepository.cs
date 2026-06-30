using Dapr.Client;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class DaprCustomerTenantRepository(DaprClient daprClient) : ITenantRepository
{
    private const string Store = "customerstore";
    private const int MaxRetries = 5;

    public async Task<TenantWorkspace?> GetAsync(string tenantId, CancellationToken ct)
    {
        var dto = await daprClient.GetStateAsync<TenantWorkspaceDto>(Store, CustomerStorageKey.Tenant(tenantId), cancellationToken: ct);
        return dto?.ToDomain();
    }

    // Enumerate tenants via the index maintained in SaveAsync (the Dapr KV store has no
    // native enumeration). Missing/stale ids are skipped so the index self-heals.
    public async Task<IReadOnlyList<TenantWorkspace>> ListAsync(CancellationToken ct)
    {
        var index = await daprClient.GetStateAsync<List<string>>(Store, CustomerStorageKey.TenantIndex(), cancellationToken: ct) ?? [];
        var tenants = new List<TenantWorkspace>(index.Count);
        foreach (var id in index)
        {
            var tenant = await GetAsync(id, ct);
            if (tenant is not null) tenants.Add(tenant);
        }
        return tenants;
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        var result = await daprClient.GetStateAsync<string>(Store, CustomerStorageKey.TenantSlug(slug), cancellationToken: ct);
        return result is not null;
    }

    public async Task<TenantWorkspace?> FindByDiscoveryDomainAsync(string domain, CancellationToken ct)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        var tenantId = await daprClient.GetStateAsync<string>(Store, CustomerStorageKey.DiscoveryDomain(normalized), cancellationToken: ct);
        if (tenantId is null) return null;

        var tenant = await GetAsync(tenantId, ct);
        if (tenant is null || !tenant.DiscoveryDomains.Any(d => d.Domain == normalized))
        {
            // Stale key left over from a previous unregister — remove and treat as not found.
            await daprClient.DeleteStateAsync(Store, CustomerStorageKey.DiscoveryDomain(normalized), cancellationToken: ct);
            return null;
        }
        return tenant;
    }

    public async Task<bool> IsDomainRegisteredAsync(string domain, string? excludeTenantId, CancellationToken ct)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        var tenantId = await daprClient.GetStateAsync<string>(Store, CustomerStorageKey.DiscoveryDomain(normalized), cancellationToken: ct);
        if (tenantId is null) return false;
        if (excludeTenantId is not null && tenantId.Equals(excludeTenantId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Verify the domain is still present on the tenant record (guards against stale keys).
        var tenant = await GetAsync(tenantId, ct);
        if (tenant is null || !tenant.DiscoveryDomains.Any(d => d.Domain == normalized))
        {
            await daprClient.DeleteStateAsync(Store, CustomerStorageKey.DiscoveryDomain(normalized), cancellationToken: ct);
            return false;
        }
        return true;
    }

    public async Task SaveAsync(TenantWorkspace tenant, CancellationToken ct)
    {
        var dto = TenantWorkspaceDto.FromDomain(tenant);
        await daprClient.SaveStateAsync(Store, CustomerStorageKey.Tenant(tenant.TenantId), dto, cancellationToken: ct);
        await daprClient.SaveStateAsync(Store, CustomerStorageKey.TenantSlug(tenant.Slug), tenant.TenantId, cancellationToken: ct);

        // Maintain the enumeration index used by the platform tenant directory (ListAsync).
        await AddToTenantIndexAsync(tenant.TenantId, ct);

        foreach (var dd in tenant.DiscoveryDomains)
            await daprClient.SaveStateAsync(Store, CustomerStorageKey.DiscoveryDomain(dd.Domain), tenant.TenantId, cancellationToken: ct);
    }

    // ETag compare-and-swap retry loop so concurrent tenant writes can't lose an index entry —
    // a plain read/modify/write would last-write-win and silently drop a tenant from the
    // directory. Mirrors the identity and tenant-request index helpers.
    private async Task AddToTenantIndexAsync(string tenantId, CancellationToken ct)
    {
        var key = CustomerStorageKey.TenantIndex();
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var (existing, etag) = await daprClient.GetStateAndETagAsync<List<string>>(Store, key, cancellationToken: ct);
            var ids = existing ?? [];
            if (ids.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
                return;
            var updated = ids.Append(tenantId).ToList();
            if (await daprClient.TrySaveStateAsync(Store, key, updated, etag, cancellationToken: ct))
                return;
            if (attempt < MaxRetries)
                await Task.Delay(20 * attempt, ct);
        }
        throw new InvalidOperationException($"Failed to update tenant index for tenant '{tenantId}' after {MaxRetries} attempts.");
    }
}
