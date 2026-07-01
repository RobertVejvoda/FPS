using FPS.Booking.Application.Repositories;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Booking.Infrastructure;

/// <summary>
/// Purges all Booking-owned data for a single tenant (PLAT003C). Booking holds operational
/// booking state, not immutable evidence, so it runs on a normal tenant purge as well as a
/// sandbox reset.
/// </summary>
public sealed class BookingTenantStorePurger(
    IBookingQueryRepository repository,
    IDrawRepository drawRepository,
    ICorrectionAuditRepository correctionAuditRepository) : ITenantStorePurger
{
    public string Service => "booking";

    public bool IsImmutableEvidence => false;

    public async Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
    {
        var requests = await repository.PurgeTenantAsync(scope.TenantId, ct);
        var draws = await drawRepository.PurgeTenantAsync(scope.TenantId, ct);
        var corrections = await correctionAuditRepository.PurgeTenantAsync(scope.TenantId, ct);
        return requests + draws + corrections;
    }
}
