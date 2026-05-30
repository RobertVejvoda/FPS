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

    public async Task SaveAsync(TenantWorkspace tenant, CancellationToken ct)
    {
        var dto = TenantWorkspaceDto.FromDomain(tenant);
        await daprClient.SaveStateAsync(Store, CustomerStorageKey.Tenant(tenant.TenantId), dto, cancellationToken: ct);
        await daprClient.SaveStateAsync(Store, CustomerStorageKey.TenantSlug(tenant.Slug), tenant.TenantId, cancellationToken: ct);
    }
}
