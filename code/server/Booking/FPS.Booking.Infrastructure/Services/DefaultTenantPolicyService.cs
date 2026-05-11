using FPS.Booking.Application.Services;

namespace FPS.Booking.Infrastructure.Services;

// Phase 1 stub — returns tenant-default policy per parking-policy-configuration.md.
// Location overrides and per-tenant storage are a future infrastructure slice.
public class DefaultTenantPolicyService : ITenantPolicyService
{
    public static readonly TenantPolicy Default = new(
        DailyRequestCap: 500,
        DrawCutOffTime: new TimeOnly(23, 59),
        TimeZoneId: "UTC",
        SameDayBookingEnabled: true,
        AllocationLookbackDays: 10,
        LateCancellationPenalty: 1,
        NoShowPenalty: 2,
        UsageConfirmationEnabled: false,
        UsageConfirmationWindowMinutes: 0,
        NoShowDetectionEnabled: false,
        SameDayUsesRequestCap: true,
        AutomaticReallocationEnabled: true,
        CompanyCarTier1Enabled: true,
        CompanyCarOverflowBehavior: CompanyCarOverflow.Reject,
        ManualAdjustmentEnabled: true,
        LateCancellationPenaltyExpiryDays: null,
        NoShowPenaltyExpiryDays: null);

    public Task<TenantPolicy> GetEffectivePolicyAsync(
        string tenantId,
        string? locationId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Default);
}
