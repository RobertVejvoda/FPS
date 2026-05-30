using Dapr.Client;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class DaprCustomerParkingBootstrapRepository(DaprClient daprClient) : ITenantParkingBootstrapRepository
{
    private const string Store = "customerstore";

    public async Task<TenantParkingBootstrap?> GetAsync(string tenantId, CancellationToken ct)
    {
        var dto = await daprClient.GetStateAsync<TenantParkingBootstrapDto>(Store, CustomerStorageKey.Bootstrap(tenantId), cancellationToken: ct);
        return dto?.ToDomain();
    }

    public async Task<TenantParkingBootstrap> GetOrCreateAsync(string tenantId, CancellationToken ct)
    {
        var existing = await GetAsync(tenantId, ct);
        if (existing is not null) return existing;
        var bootstrap = new TenantParkingBootstrap { TenantId = tenantId };
        await SaveAsync(bootstrap, ct);
        return bootstrap;
    }

    public async Task SaveAsync(TenantParkingBootstrap bootstrap, CancellationToken ct)
    {
        var dto = TenantParkingBootstrapDto.FromDomain(bootstrap);
        await daprClient.SaveStateAsync(Store, CustomerStorageKey.Bootstrap(bootstrap.TenantId), dto, cancellationToken: ct);
    }
}
