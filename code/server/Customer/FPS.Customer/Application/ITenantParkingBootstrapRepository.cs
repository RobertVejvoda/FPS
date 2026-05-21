using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public interface ITenantParkingBootstrapRepository
{
    Task<TenantParkingBootstrap> GetOrCreateAsync(string tenantId, CancellationToken ct);
    Task SaveAsync(TenantParkingBootstrap bootstrap, CancellationToken ct);
}
