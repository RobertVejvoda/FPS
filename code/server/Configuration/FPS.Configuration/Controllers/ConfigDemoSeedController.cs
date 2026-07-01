using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace FPS.Configuration.Controllers;

// Internal demo-seed endpoint — replaces slots and policy for a location within a sandbox or
// evaluation tenant. Not in the OpenAPI spec (IgnoreApi = true). PLAT003C-C2: gated by
// [DaprInternalOnly] (the dapr-api-token boundary), so gateway-routed external traffic can't reach it.
// The tenant is taken from the request body (a scheduled reset has no operator JWT) and shape-validated
// before persistence. When an operator JWT is present its user id is used for publish attribution;
// otherwise a system "demo-seed" actor is used.
[ApiController]
[Route("configuration/admin")]
[DaprInternalOnly]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ConfigDemoSeedController(
    ParkingSlotService slotService,
    ParkingPolicyService policyService,
    ICurrentUser currentUser) : ControllerBase
{
    private const string SeedActor = "demo-seed";

    [HttpPost("demo-seed")]
    public async Task<IActionResult> DemoSeed(
        [FromBody] ConfigDemoSeedRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return BadRequest(new { error = "TenantId is required." });

        try { TenantStorageKey.Sanitise(request.TenantId); }
        catch (ArgumentException) { return BadRequest(new { error = "Invalid tenant id." }); }

        var tenantId = request.TenantId;
        var actor = currentUser.IsAuthenticated && !string.IsNullOrEmpty(currentUser.UserId)
            ? currentUser.UserId
            : SeedActor;

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
            tenantId, request.LocationId, slots, actor, "demo-seed", ct);
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
            PublishedByUserId = actor,
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
    string TenantId,
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
