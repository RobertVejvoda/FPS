using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace FPS.Profile.Controllers;

// Internal demo-seed endpoint — upserts a batch of employee profiles for a sandbox or evaluation
// tenant. Not in the OpenAPI spec (IgnoreApi = true). PLAT003C-C2: gated by [DaprInternalOnly] (the
// dapr-api-token boundary the /erasure and pub/sub endpoints use), so gateway-routed external traffic
// can't reach it. The tenant is taken from the request body because a scheduled sandbox reset has no
// operator JWT; the id is shape-validated before any persistence so a malformed id returns 400.
[ApiController]
[Route("profile/admin")]
[DaprInternalOnly]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ProfileDemoSeedController(
    IProfileRepository repository,
    IDeactivatedUserStore deactivatedUserStore) : ControllerBase
{
    [HttpPost("demo-seed")]
    public async Task<IActionResult> DemoSeed(
        [FromBody] ProfileDemoSeedRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return BadRequest(new { error = "TenantId is required." });

        try { TenantStorageKey.Sanitise(request.TenantId); }
        catch (ArgumentException) { return BadRequest(new { error = "Invalid tenant id." }); }

        var tenantId = request.TenantId;
        var seeded = 0;

        foreach (var emp in request.Employees)
        {
            var profile = Build(tenantId, emp);
            await repository.SaveAsync(profile, ct);
            deactivatedUserStore.Reactivate(tenantId, emp.UserId);
            seeded++;
        }

        return Ok(new { profilesSeeded = seeded });
    }

    private static UserProfile Build(string tenantId, DemoEmployeeSpec emp)
    {
        var vehicles = NormalizeDefaults(
            emp.Vehicles.Select(v => new Vehicle(
                v.VehicleId, v.LicensePlate, v.VehicleType,
                v.IsElectric, IsActive: true, IsDefault: v.IsDefault)).ToList());

        return new UserProfile
        {
            TenantId = tenantId,
            UserId = emp.UserId,
            DisplayName = emp.DisplayName,
            Status = ProfileStatus.Active,
            FpsRoles = emp.FpsRoles,
            NotificationAddress = emp.NotificationAddress,
            HomeLocationId = emp.HomeLocationId,
            ParkingEligible = emp.ParkingEligible,
            HasCompanyCar = emp.HasCompanyCar,
            AccessibilityEligible = emp.AccessibilityEligible,
            ReservedSpaceEligible = emp.ReservedSpaceEligible,
            Vehicles = vehicles,
            SnapshotVersion = "demo-seed-v1",
            FactSource = "demo-seed",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static IReadOnlyList<Vehicle> NormalizeDefaults(List<Vehicle> vehicles)
    {
        if (vehicles.Count == 0) return vehicles;
        var activeDefault = vehicles.FirstOrDefault(v => v.IsActive && v.IsDefault)
            ?? vehicles.FirstOrDefault(v => v.IsActive);
        return vehicles
            .Select(v => v with { IsDefault = v.IsActive && v.VehicleId == activeDefault?.VehicleId })
            .ToList();
    }
}

public sealed record ProfileDemoSeedRequest(string TenantId, IReadOnlyList<DemoEmployeeSpec> Employees);

public sealed record DemoEmployeeSpec(
    string UserId,
    string? DisplayName,
    IReadOnlyList<string> FpsRoles,
    string? NotificationAddress,
    string? HomeLocationId,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible,
    IReadOnlyList<DemoVehicleSpec> Vehicles);

public sealed record DemoVehicleSpec(
    string VehicleId,
    string LicensePlate,
    string VehicleType,
    bool IsElectric,
    bool IsDefault = false);
