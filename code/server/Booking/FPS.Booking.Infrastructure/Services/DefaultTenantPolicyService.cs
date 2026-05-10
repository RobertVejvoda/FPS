using FPS.Booking.Application.Services;

namespace FPS.Booking.Infrastructure.Services;

// Returns tenant default policy values from parking-policy-configuration.md.
// Per-tenant/location overrides are a future configuration slice.
public class DefaultTenantPolicyService : ITenantPolicyService
{
    public Task<TenantPolicy> GetEffectivePolicyAsync(
        string tenantId,
        string? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var policy = new TenantPolicy(
            DailyRequestCap: 500,
            DrawCutOffTime: new TimeOnly(18, 0),
            TimeZoneId: "UTC",
            SameDayBookingEnabled: true,
            AllocationLookbackDays: 10,
            LateCancellationPenalty: 1,
            NoShowPenalty: 2);

        return Task.FromResult(policy);
    }
}
