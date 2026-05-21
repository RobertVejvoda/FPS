using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public interface ITenantRepository
{
    Task<TenantWorkspace?> GetAsync(string tenantId, CancellationToken ct);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);
    Task SaveAsync(TenantWorkspace tenant, CancellationToken ct);
}
