using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

[ApiController]
[Route("profile/vehicles")]
[Authorize(Roles = "employee")]
public sealed class EmployeeVehicleController(
    IProfileRepository repository,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AddVehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddVehicle(
        [FromBody] AddVehicleRequest request,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var profile = await repository.GetAsync(currentUser.TenantId, currentUser.UserId, ct);
        if (profile is null) return NotFound(new { error = "Profile not found." });

        if (string.IsNullOrWhiteSpace(request.LicensePlate))
            return BadRequest(new { error = "License plate is required." });

        if (string.IsNullOrWhiteSpace(request.VehicleType))
            return BadRequest(new { error = "Vehicle type is required." });

        var vehicleId = Guid.NewGuid().ToString();
        var isFirstActive = profile.ActiveVehicles.Count == 0;
        var newVehicle = new Vehicle(vehicleId, request.LicensePlate.Trim().ToUpperInvariant(),
            request.VehicleType, request.IsElectric, IsActive: true, IsDefault: isFirstActive);

        await repository.SaveAsync(WithVehicles(profile, [.. profile.Vehicles, newVehicle]), ct);
        return Ok(new AddVehicleResponse(vehicleId));
    }

    [HttpDelete("{vehicleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveVehicle(string vehicleId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var profile = await repository.GetAsync(currentUser.TenantId, currentUser.UserId, ct);
        if (profile is null) return NotFound(new { error = "Profile not found." });

        var vehicle = profile.Vehicles.FirstOrDefault(v => v.VehicleId == vehicleId);
        if (vehicle is null) return NotFound(new { error = "Vehicle not found." });

        var remaining = profile.Vehicles
            .Select(v => v.VehicleId == vehicleId ? new Vehicle(v.VehicleId, v.LicensePlate, v.VehicleType, v.IsElectric, IsActive: false, IsDefault: false) : v)
            .ToList();

        // Promote next active vehicle as default if the removed one was default.
        if (vehicle.IsDefault)
        {
            var next = remaining.FirstOrDefault(v => v.IsActive);
            if (next is not null)
                remaining = remaining.Select(v => v.VehicleId == next.VehicleId ? new Vehicle(v.VehicleId, v.LicensePlate, v.VehicleType, v.IsElectric, v.IsActive, IsDefault: true) : v).ToList();
        }

        await repository.SaveAsync(WithVehicles(profile, remaining), ct);
        return NoContent();
    }

    [HttpPut("{vehicleId}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(string vehicleId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var profile = await repository.GetAsync(currentUser.TenantId, currentUser.UserId, ct);
        if (profile is null) return NotFound(new { error = "Profile not found." });

        var vehicle = profile.Vehicles.FirstOrDefault(v => v.VehicleId == vehicleId && v.IsActive);
        if (vehicle is null) return NotFound(new { error = "Active vehicle not found." });

        var newVehicles = profile.Vehicles
            .Select(v => new Vehicle(v.VehicleId, v.LicensePlate, v.VehicleType, v.IsElectric, v.IsActive, IsDefault: v.IsActive && v.VehicleId == vehicleId))
            .ToList();

        await repository.SaveAsync(WithVehicles(profile, newVehicles), ct);
        return NoContent();
    }

    private static string NextVersion(string current) =>
        int.TryParse(current, out var n) ? (n + 1).ToString() : current + "-1";

    private static UserProfile WithVehicles(UserProfile p, IReadOnlyList<Vehicle> vehicles) => new()
    {
        TenantId = p.TenantId,
        UserId = p.UserId,
        Status = p.Status,
        ParkingEligible = p.ParkingEligible,
        HasCompanyCar = p.HasCompanyCar,
        AccessibilityEligible = p.AccessibilityEligible,
        ReservedSpaceEligible = p.ReservedSpaceEligible,
        EmployeeId = p.EmployeeId,
        FpsRoles = p.FpsRoles,
        NotificationAddress = p.NotificationAddress,
        HomeLocationId = p.HomeLocationId,
        Vehicles = vehicles,
        SnapshotVersion = NextVersion(p.SnapshotVersion),
        FactSource = p.FactSource,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}

public sealed record AddVehicleRequest(
    string LicensePlate,
    string VehicleType,
    bool IsElectric);

public sealed record AddVehicleResponse(string VehicleId);
