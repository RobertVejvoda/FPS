using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;

namespace FPS.Configuration.Controllers;

// Internal demo-seed endpoint — replaces slots and policy for a location within a
// sandbox or evaluation tenant. Not in the OpenAPI spec (IgnoreApi = true). The
// caller (Customer service) validates TenantKind before issuing this request.
[ApiController]
[Route("configuration/admin")]
[Authorize(Roles = "admin")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ConfigDemoSeedController(
    ParkingSlotService slotService,
    ParkingPolicyService policyService,
    ICurrentUser currentUser,
    IConfiguration config) : ControllerBase
{
    [HttpPost("demo-seed")]
    public async Task<IActionResult> DemoSeed(
        [FromBody] ConfigDemoSeedRequest request,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var expectedKey = config["DemoSeed:InternalKey"];
        if (!string.IsNullOrEmpty(expectedKey))
        {
            var providedKey = HttpContext.Request.Headers["X-FPS-Seed-Key"].ToString();
            if (providedKey != expectedKey)
                return Unauthorized();
        }

        var tenantId = currentUser.TenantId;

        var slots = request.Slots
            .Select(s => new ParkingSlot
            {
                SlotId = s.SlotId,
                TenantId = tenantId,
                LocationId = request.LocationId,
                IsActive = s.IsActive,
                HasCharger = s.HasCharger,
                IsAccessible = s.IsAccessible,
                IsCompanyCarOnly = s.IsCompanyCarOnly,
                IsMotorcycleCapacity = s.IsMotorcycleCapacity,
                MotorcycleCapacityUnits = s.MotorcycleCapacityUnits,
                ReservedForUserId = s.ReservedForUserId,
            })
            .ToList();

        var slotErrors = await slotService.ReplaceAsync(
            tenantId, request.LocationId, slots, currentUser.UserId, "demo-seed", ct);
        if (slotErrors.Count > 0)
            return BadRequest(new { errors = slotErrors });

        var policy = new ParkingPolicy
        {
            TenantId = tenantId,
            TimeZone = request.Policy.TimeZone,
            DrawCutOffTime = request.Policy.DrawCutOffTime,
            DailyRequestCap = request.Policy.DailyRequestCap,
            AllocationLookbackDays = request.Policy.AllocationLookbackDays,
            LateCancellationPenalty = request.Policy.LateCancellationPenalty,
            NoShowPenalty = request.Policy.NoShowPenalty,
            ManualAdjustmentEnabled = request.Policy.ManualAdjustmentEnabled,
            SameDayBookingEnabled = request.Policy.SameDayBookingEnabled,
            SameDayUsesRequestCap = request.Policy.SameDayUsesRequestCap,
            AutomaticReallocationEnabled = request.Policy.AutomaticReallocationEnabled,
            UsageConfirmationRequired = request.Policy.UsageConfirmationRequired,
            UsageConfirmationWindowMinutes = request.Policy.UsageConfirmationWindowMinutes,
            UsageConfirmationMethods = request.Policy.UsageConfirmationMethods,
            NoShowDetectionEnabled = request.Policy.NoShowDetectionEnabled,
            CompanyCarTier1Enabled = request.Policy.CompanyCarTier1Enabled,
            CompanyCarOverflowBehavior = request.Policy.CompanyCarOverflowBehavior,
            PublishedByUserId = currentUser.UserId,
            PublishedAt = DateTimeOffset.UtcNow,
            PublicationReason = "demo-seed",
        };

        var policyErrors = await policyService.SaveTenantDefaultAsync(policy, ct);
        if (policyErrors.Count > 0)
            return BadRequest(new { errors = policyErrors });

        return Ok(new { slotsSeeded = slots.Count, policyUpdated = true });
    }
}

public sealed record ConfigDemoSeedRequest(
    string LocationId,
    IReadOnlyList<DemoSlotSpec> Slots,
    DemoPolicySpec Policy);

public sealed record DemoSlotSpec(
    string SlotId,
    bool IsActive,
    bool HasCharger,
    bool IsAccessible,
    bool IsCompanyCarOnly,
    bool IsMotorcycleCapacity,
    string? ReservedForUserId,
    int? MotorcycleCapacityUnits = null);

public sealed record DemoPolicySpec(
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
    string CompanyCarOverflowBehavior);
