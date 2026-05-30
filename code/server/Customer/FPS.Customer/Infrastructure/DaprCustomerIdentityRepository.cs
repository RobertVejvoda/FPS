using Dapr.Client;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class DaprCustomerIdentityRepository(DaprClient daprClient) : ITenantIdentityRepository
{
    private const string Store = "customerstore";

    public async Task<TenantIdentityConfig?> GetConfigAsync(string tenantId, CancellationToken ct)
    {
        var dto = await daprClient.GetStateAsync<TenantIdentityConfigDto>(Store, CustomerStorageKey.IdentityConfig(tenantId), cancellationToken: ct);
        return dto?.ToDomain();
    }

    public async Task SaveConfigAsync(TenantIdentityConfig config, CancellationToken ct)
    {
        var dto = TenantIdentityConfigDto.FromDomain(config);
        await daprClient.SaveStateAsync(Store, CustomerStorageKey.IdentityConfig(config.TenantId), dto, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TenantAdminRecord>> GetAdminsAsync(string tenantId, CancellationToken ct)
    {
        var list = await daprClient.GetStateAsync<List<TenantAdminRecord>>(Store, CustomerStorageKey.IdentityAdmins(tenantId), cancellationToken: ct);
        return list ?? [];
    }

    public async Task SaveAdminAsync(TenantAdminRecord admin, CancellationToken ct)
    {
        var key = CustomerStorageKey.IdentityAdmins(admin.TenantId);
        var existing = await daprClient.GetStateAsync<List<TenantAdminRecord>>(Store, key, cancellationToken: ct) ?? [];
        existing.Add(admin);
        await daprClient.SaveStateAsync(Store, key, existing, cancellationToken: ct);
    }
}
