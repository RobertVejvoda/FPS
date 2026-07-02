namespace FPS.Booking.Application.Services;

// PLAT-seats (#710) — resolves which product modules a tenant has enabled, so submission can
// enforce the module boundary server-side (a Seats request is only accepted for a Seats-enabled
// tenant). Implementations fail closed to Parking-only when the tenant cannot be confirmed.
public interface ITenantModulesService
{
    Task<IReadOnlyList<string>> GetEnabledModulesAsync(string tenantId, CancellationToken cancellationToken = default);
}
