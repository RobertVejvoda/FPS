using FPS.Profile.Domain;

namespace FPS.Profile.Application;

public interface IProfileRepository
{
    Task<UserProfile?> GetAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
    Task<bool> EmployeeIdExistsAsync(string tenantId, string employeeId, CancellationToken cancellationToken = default);
    Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default);

    // Issue #533: tenant-scoped enumeration used by the company-car capacity
    // warning. Kept as a single bulk read so the in-memory store stays simple
    // and the future Dapr/MongoDB swap is just one query per tenant.
    Task<IReadOnlyList<UserProfile>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
