using Dapr.Client;
using FPS.Customer.Application;
using FPS.Customer.Domain;

namespace FPS.Customer.Infrastructure;

public sealed class DaprCustomerIdentityRepository(DaprClient daprClient) : ITenantIdentityRepository
{
    private const string Store = "customerstore";
    private const int MaxRetries = 5;

    public async Task<TenantIdentityConfig?> GetConfigAsync(string tenantId, CancellationToken ct)
    {
        var dto = await daprClient.GetStateAsync<TenantIdentityConfigDto>(Store, CustomerStorageKey.IdentityConfig(tenantId), cancellationToken: ct);
        return dto?.ToDomain();
    }

    public async Task SaveConfigAsync(TenantIdentityConfig config, CancellationToken ct)
    {
        var dto = TenantIdentityConfigDto.FromDomain(config);
        await daprClient.SaveStateAsync(Store, CustomerStorageKey.IdentityConfig(config.TenantId), dto, cancellationToken: ct);
        await AddToIdentityIndexAsync(config.TenantId, ct);
    }

    public async Task<IReadOnlyList<TenantAdminRecord>> GetAdminsAsync(string tenantId, CancellationToken ct)
    {
        var list = await daprClient.GetStateAsync<List<TenantAdminRecord>>(Store, CustomerStorageKey.IdentityAdmins(tenantId), cancellationToken: ct);
        return list ?? [];
    }

    public async Task SaveAdminAsync(TenantAdminRecord admin, CancellationToken ct)
    {
        var key = CustomerStorageKey.IdentityAdmins(admin.TenantId);
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var (existing, etag) = await daprClient.GetStateAndETagAsync<List<TenantAdminRecord>>(Store, key, cancellationToken: ct);
            var updated = (existing ?? []).Append(admin).ToList();
            if (await daprClient.TrySaveStateAsync(Store, key, updated, etag, cancellationToken: ct))
                return;
            if (attempt < MaxRetries)
                await Task.Delay(20 * attempt, ct);
        }
        throw new InvalidOperationException($"Failed to save admin for tenant '{admin.TenantId}' after {MaxRetries} attempts.");
    }

    public async Task<IReadOnlyList<string>> GetConfiguredTenantIdsAsync(CancellationToken ct)
    {
        var list = await daprClient.GetStateAsync<List<string>>(Store, CustomerStorageKey.IdentityIndex(), cancellationToken: ct);
        return list ?? [];
    }

    private async Task AddToIdentityIndexAsync(string tenantId, CancellationToken ct)
    {
        var key = CustomerStorageKey.IdentityIndex();
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
        throw new InvalidOperationException($"Failed to update identity index for tenant '{tenantId}' after {MaxRetries} attempts.");
    }
}
