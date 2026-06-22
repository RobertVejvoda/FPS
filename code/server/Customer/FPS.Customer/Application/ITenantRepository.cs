using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public interface ITenantRepository
{
    Task<TenantWorkspace?> GetAsync(string tenantId, CancellationToken ct);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
    Task SaveAsync(TenantWorkspace tenant, CancellationToken ct);
    Task<TenantWorkspace?> FindByDiscoveryDomainAsync(string domain, CancellationToken ct);
    Task<bool> IsDomainRegisteredAsync(string domain, string? excludeTenantId, CancellationToken ct);
}
