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
            SameDayBookingEnabled: true);

        return Task.FromResult(policy);
    }
}
