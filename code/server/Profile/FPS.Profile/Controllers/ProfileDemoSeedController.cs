using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;

namespace FPS.Profile.Controllers;

// Internal demo-seed endpoint — upserts a batch of employee profiles for a sandbox
// or evaluation tenant. Not in the OpenAPI spec (IgnoreApi = true). The caller
// (Customer service) validates TenantKind before issuing this request; this
// endpoint only re-checks that the actor is an authenticated admin.
[ApiController]
[Route("profile/admin")]
[Authorize(Roles = "admin")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ProfileDemoSeedController(
    IProfileRepository repository,
    IDeactivatedUserStore deactivatedUserStore,
    ICurrentUser currentUser,
    IConfiguration config) : ControllerBase
{
    [HttpPost("demo-seed")]
    public async Task<IActionResult> DemoSeed(
        [FromBody] ProfileDemoSeedRequest request,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var expectedKey = config["DemoSeed:InternalKey"];
        if (!string.IsNullOrEmpty(expectedKey))
        {
            var providedKey = HttpContext.Request.Headers["X-FPS-Seed-Key"].ToString();
            if (providedKey != expectedKey)
                return Unauthorized();
        }

        var tenantId = currentUser.TenantId;
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

public sealed record ProfileDemoSeedRequest(IReadOnlyList<DemoEmployeeSpec> Employees);

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
