using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Configuration.Controllers;

[ApiController]
[Authorize(Roles = $"{ConfigurationRoles.Admin},{ConfigurationRoles.HrManager}")]
public sealed class ParkingPolicyController(ParkingPolicyService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/configuration/parking-policy")]
    public async Task<IActionResult> GetTenantDefault(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var policy = await service.GetTenantDefaultAsync(currentUser.TenantId, ct);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpPut("/configuration/parking-policy")]
    public async Task<IActionResult> PutTenantDefault([FromBody] PutParkingPolicyRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var policy = request.ToDomain(currentUser.TenantId, null, currentUser.UserId);
        var errors = await service.SaveTenantDefaultAsync(policy, ct);
        return errors.Count > 0 ? BadRequest(new { errors }) : NoContent();
    }

    [HttpGet("/configuration/locations/{locationId}/parking-policy")]
    public async Task<IActionResult> GetLocationEffectivePolicy(string locationId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var policy = await service.GetEffectivePolicyAsync(currentUser.TenantId, locationId, ct);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpPut("/configuration/locations/{locationId}/parking-policy")]
    public async Task<IActionResult> PutLocationOverride(string locationId, [FromBody] PutParkingPolicyRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var policy = request.ToDomain(currentUser.TenantId, locationId, currentUser.UserId);
        var errors = await service.SaveLocationOverrideAsync(policy, ct);
        return errors.Count > 0 ? BadRequest(new { errors }) : NoContent();
    }
}

public sealed record PutParkingPolicyRequest(
    string TimeZone,
    TimeOnly DrawCutOffTime,
    int DailyRequestCap,
    int AllocationLookbackDays,
    int LateCancellationPenalty,
    int NoShowPenalty,
    bool ManualAdjustmentEnabled,
    bool SameDayBookingEnabled,
    bool SameDayUsesRequestCap,
    bool AutomaticReallocationEnabled,
    bool UsageConfirmationRequired,
    int UsageConfirmationWindowMinutes,
    IReadOnlyList<string> UsageConfirmationMethods,
    bool NoShowDetectionEnabled,
    bool CompanyCarTier1Enabled,
    string CompanyCarOverflowBehavior,
    string? PublicationReason)
{
    internal ParkingPolicy ToDomain(string tenantId, string? locationId, string publishedByUserId) =>
        new()
        {
            TenantId = tenantId,
            LocationId = locationId,
            TimeZone = TimeZone,
            DrawCutOffTime = DrawCutOffTime,
            DailyRequestCap = DailyRequestCap,
            AllocationLookbackDays = AllocationLookbackDays,
            LateCancellationPenalty = LateCancellationPenalty,
            NoShowPenalty = NoShowPenalty,
            ManualAdjustmentEnabled = ManualAdjustmentEnabled,
            SameDayBookingEnabled = SameDayBookingEnabled,
            SameDayUsesRequestCap = SameDayUsesRequestCap,
            AutomaticReallocationEnabled = AutomaticReallocationEnabled,
            UsageConfirmationRequired = UsageConfirmationRequired,
            UsageConfirmationWindowMinutes = UsageConfirmationWindowMinutes,
            UsageConfirmationMethods = UsageConfirmationMethods,
            NoShowDetectionEnabled = NoShowDetectionEnabled,
            CompanyCarTier1Enabled = CompanyCarTier1Enabled,
            CompanyCarOverflowBehavior = CompanyCarOverflowBehavior,
            PublishedByUserId = publishedByUserId,
            PublishedAt = DateTimeOffset.UtcNow,
            PublicationReason = PublicationReason
        };
}

internal static class ConfigurationRoles
{
    internal const string Admin = "admin";
    internal const string HrManager = "hr_manager";
}
