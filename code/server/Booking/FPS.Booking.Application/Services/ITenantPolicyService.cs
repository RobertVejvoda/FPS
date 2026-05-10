namespace FPS.Booking.Application.Services;

public record TenantPolicy(
    int DailyRequestCap,
    TimeOnly DrawCutOffTime,
    string TimeZoneId,
    bool SameDayBookingEnabled,
    int AllocationLookbackDays = 10);

public interface ITenantPolicyService
{
    Task<TenantPolicy> GetEffectivePolicyAsync(string tenantId, string? locationId = null, CancellationToken cancellationToken = default);
}
