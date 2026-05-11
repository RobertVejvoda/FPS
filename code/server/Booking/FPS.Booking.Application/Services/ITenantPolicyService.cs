namespace FPS.Booking.Application.Services;

public enum CompanyCarOverflow { Reject }

public sealed record TenantPolicy(
    int DailyRequestCap,
    TimeOnly DrawCutOffTime,
    string TimeZoneId,
    bool SameDayBookingEnabled,
    int AllocationLookbackDays = 10,
    int LateCancellationPenalty = 1,
    int NoShowPenalty = 2,
    bool UsageConfirmationEnabled = false,
    int UsageConfirmationWindowMinutes = 0,
    bool NoShowDetectionEnabled = false,
    bool SameDayUsesRequestCap = true,
    bool AutomaticReallocationEnabled = true,
    bool CompanyCarTier1Enabled = true,
    CompanyCarOverflow CompanyCarOverflowBehavior = CompanyCarOverflow.Reject,
    bool ManualAdjustmentEnabled = true,
    int? LateCancellationPenaltyExpiryDays = null,   // null = use AllocationLookbackDays
    int? NoShowPenaltyExpiryDays = null)              // null = use AllocationLookbackDays
{
    public int EffectiveLateCancellationPenaltyExpiry => LateCancellationPenaltyExpiryDays ?? AllocationLookbackDays;
    public int EffectiveNoShowPenaltyExpiry => NoShowPenaltyExpiryDays ?? AllocationLookbackDays;

    public void Validate()
    {
        if (NoShowDetectionEnabled && !UsageConfirmationEnabled)
            throw new InvalidOperationException(
                "NoShowDetectionEnabled requires UsageConfirmationEnabled. Enable a confirmation method first.");
        if (DailyRequestCap < 1)
            throw new InvalidOperationException("DailyRequestCap must be at least 1.");
        if (AllocationLookbackDays < 1)
            throw new InvalidOperationException("AllocationLookbackDays must be at least 1.");
    }

    /// <summary>
    /// Returns a new policy with the given location overrides applied on top of this tenant default.
    /// </summary>
    public TenantPolicy WithLocationOverride(LocationPolicyOverride? loc)
    {
        if (loc is null) return this;
        return this with
        {
            DrawCutOffTime = loc.DrawCutOffTime ?? DrawCutOffTime,
            TimeZoneId = loc.TimeZoneId ?? TimeZoneId,
            DailyRequestCap = loc.DailyRequestCap ?? DailyRequestCap,
            AllocationLookbackDays = loc.AllocationLookbackDays ?? AllocationLookbackDays,
            SameDayBookingEnabled = loc.SameDayBookingEnabled ?? SameDayBookingEnabled,
            UsageConfirmationEnabled = loc.UsageConfirmationEnabled ?? UsageConfirmationEnabled,
            UsageConfirmationWindowMinutes = loc.UsageConfirmationWindowMinutes ?? UsageConfirmationWindowMinutes,
            NoShowDetectionEnabled = loc.NoShowDetectionEnabled ?? NoShowDetectionEnabled,
            AutomaticReallocationEnabled = loc.AutomaticReallocationEnabled ?? AutomaticReallocationEnabled,
            CompanyCarOverflowBehavior = loc.CompanyCarOverflowBehavior ?? CompanyCarOverflowBehavior,
            LateCancellationPenalty = loc.LateCancellationPenalty ?? LateCancellationPenalty,
            NoShowPenalty = loc.NoShowPenalty ?? NoShowPenalty,
            LateCancellationPenaltyExpiryDays = loc.LateCancellationPenaltyExpiryDays ?? LateCancellationPenaltyExpiryDays,
            NoShowPenaltyExpiryDays = loc.NoShowPenaltyExpiryDays ?? NoShowPenaltyExpiryDays,
        };
    }
}

/// <summary>
/// Per-location overrides. Only set fields override the tenant default; null fields fall back.
/// </summary>
public sealed record LocationPolicyOverride(
    TimeOnly? DrawCutOffTime = null,
    string? TimeZoneId = null,
    int? DailyRequestCap = null,
    int? AllocationLookbackDays = null,
    bool? SameDayBookingEnabled = null,
    bool? UsageConfirmationEnabled = null,
    int? UsageConfirmationWindowMinutes = null,
    bool? NoShowDetectionEnabled = null,
    bool? AutomaticReallocationEnabled = null,
    CompanyCarOverflow? CompanyCarOverflowBehavior = null,
    int? LateCancellationPenalty = null,
    int? NoShowPenalty = null,
    int? LateCancellationPenaltyExpiryDays = null,
    int? NoShowPenaltyExpiryDays = null);

public interface ITenantPolicyService
{
    Task<TenantPolicy> GetEffectivePolicyAsync(string tenantId, string? locationId = null, CancellationToken cancellationToken = default);
}
