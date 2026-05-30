using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public interface ITenantIdentityRepository
{
    Task<TenantIdentityConfig?> GetConfigAsync(string tenantId, CancellationToken ct);
    Task SaveConfigAsync(TenantIdentityConfig config, CancellationToken ct);
    Task<IReadOnlyList<TenantAdminRecord>> GetAdminsAsync(string tenantId, CancellationToken ct);
    Task SaveAdminAsync(TenantAdminRecord admin, CancellationToken ct);
    Task<IReadOnlyList<string>> GetConfiguredTenantIdsAsync(CancellationToken ct);
}
