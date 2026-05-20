using FPS.Profile.Application;
using FPS.Profile.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace FPS.Profile.Controllers;

// Local-development seed endpoint — returns 404 outside Development.
// Used by tools/dev-seed.sh to write profile snapshots without requiring
// real OIDC claims or a full HR data import pipeline.
// IgnoreApi = true keeps this endpoint out of the OpenAPI spec and generated clients.
[ApiController]
[Route("profile/admin")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ProfileAdminController(
    IProfileRepository repository,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpPut("snapshot")]
    public async Task<IActionResult> SeedSnapshot(
        [FromBody] SeedProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!env.IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest("tenantId and userId are required.");

        var profile = new UserProfile
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            Status = ProfileStatus.Active,
            ParkingEligible = request.ParkingEligible,
            HasCompanyCar = request.HasCompanyCar,
            AccessibilityEligible = request.AccessibilityEligible,
            ReservedSpaceEligible = request.ReservedSpaceEligible,
            Vehicles = request.Vehicles
                .Select(v => new Vehicle(v.VehicleId, v.LicensePlate, v.VehicleType, v.IsElectric, v.IsActive))
                .ToList(),
            SnapshotVersion = "seed-v1",
        };

        await repository.SaveAsync(profile, cancellationToken);
        return NoContent();
    }
}

public sealed record SeedProfileRequest(
    string TenantId,
    string UserId,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible,
    IReadOnlyList<SeedVehicleRequest> Vehicles);

public sealed record SeedVehicleRequest(
    string VehicleId,
    string LicensePlate,
    string VehicleType,
    bool IsElectric,
    bool IsActive);
