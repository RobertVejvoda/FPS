using Dapr.Client;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class DaprCustomerTenantRepository(DaprClient daprClient) : ITenantRepository
{
    private const string Store = "customerstore";

    public async Task<TenantWorkspace?> GetAsync(string tenantId, CancellationToken ct)
    {
        var dto = await daprClient.GetStateAsync<TenantWorkspaceDto>(Store, CustomerStorageKey.Tenant(tenantId), cancellationToken: ct);
        return dto?.ToDomain();
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

        foreach (var dd in tenant.DiscoveryDomains)
            await daprClient.SaveStateAsync(Store, CustomerStorageKey.DiscoveryDomain(dd.Domain), tenant.TenantId, cancellationToken: ct);
    }
}
