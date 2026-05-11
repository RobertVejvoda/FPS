using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

[ApiController]
[Route("profile/snapshot")]
[Authorize]
public sealed class ProfileSnapshotController(
    IProfileRepository repository,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet(Name = "GetProfileSnapshot")]
    [ProducesResponseType(typeof(ProfileSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var profile = await repository.GetAsync(currentUser.TenantId, currentUser.UserId, cancellationToken);

        if (profile is null)
            return NotFound();

        return Ok(ToSnapshot(profile));
    }

    private static ProfileSnapshot ToSnapshot(UserProfile p) => new(
        TenantId: p.TenantId,
        UserId: p.UserId,
        ProfileStatus: p.Status.ToString(),
        ParkingEligible: p.ParkingEligible,
        HasCompanyCar: p.HasCompanyCar,
        AccessibilityEligible: p.AccessibilityEligible,
        ReservedSpaceEligible: p.ReservedSpaceEligible,
        Vehicles: p.ActiveVehicles.Select(v => new VehicleSnapshot(
            v.VehicleId, v.LicensePlate, v.VehicleType, v.IsElectric, v.IsActive)).ToList(),
        SnapshotVersion: p.SnapshotVersion);
}
