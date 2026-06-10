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
    public async Task<IActionResult> GetSnapshot(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var profile = await repository.GetAsync(currentUser.TenantId, currentUser.UserId, cancellationToken);

        if (profile is null)
        {
            // SSO-first: provision a minimal profile from authenticated claims on first access.
            // Policy-sensitive facts (eligibility, vehicles) default to false/empty and are
            // updated by admin entry or authorized import after provisioning.
            profile = ProvisionFromClaims();
            await repository.SaveAsync(profile, cancellationToken);
        }

        return Ok(ToSnapshot(profile));
    }

    private UserProfile ProvisionFromClaims() => new()
    {
        TenantId = currentUser.TenantId,
        UserId = currentUser.UserId,
        Status = ProfileStatus.Active,
        ParkingEligible = false,
        HasCompanyCar = false,
        AccessibilityEligible = false,
        ReservedSpaceEligible = false,
        Vehicles = [],
        SnapshotVersion = "1",
        FactSource = "sso-claims",
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static ProfileSnapshot ToSnapshot(UserProfile p) => new(
        TenantId: p.TenantId,
        UserId: p.UserId,
        ProfileStatus: p.Status.ToString(),
        ParkingEligible: p.ParkingEligible,
        HasCompanyCar: p.HasCompanyCar,
        AccessibilityEligible: p.AccessibilityEligible,
        ReservedSpaceEligible: p.ReservedSpaceEligible,
        Vehicles: p.ActiveVehicles.Select(v => new VehicleSnapshot(
            v.VehicleId, v.LicensePlate, v.VehicleType, v.IsElectric, v.IsActive, v.IsDefault)).ToList(),
        SnapshotVersion: p.SnapshotVersion,
        DisplayName: p.DisplayName);
}
