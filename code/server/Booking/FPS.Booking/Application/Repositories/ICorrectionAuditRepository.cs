using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Repositories;

public interface ICorrectionAuditRepository
{
    Task SaveAsync(CorrectionAuditDto audit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Destructively purges every correction audit for the tenant (PLAT003C) via the per-tenant
    /// correction index, then removes the index itself. Returns the number of records deleted.
    /// Idempotent: a re-run over an already-purged tenant returns 0.
    /// </summary>
    Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
