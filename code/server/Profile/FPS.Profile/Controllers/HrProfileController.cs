using FPS.Profile.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

[ApiController]
[Route("profile/hr")]
[Authorize(Roles = "hr_manager,admin")]
public sealed class HrProfileController(
    IProfileRepository repository,
    ICurrentUser currentUser) : ControllerBase
{
    private const int MaxBatchSize = 200;

    /// <summary>
    /// Returns display names for a batch of subject hashes.
    /// Restricted to HR and admin roles. Names are never exposed on employee screens or audit payloads.
    /// </summary>
    [HttpPost("display-names")]
    [ProducesResponseType(typeof(DisplayNamesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDisplayNames(
        [FromBody] DisplayNamesRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        if (request.UserIds is null)
            return BadRequest("userIds is required.");

        if (request.UserIds.Count > MaxBatchSize)
            return BadRequest($"Batch size must not exceed {MaxBatchSize}.");

        var names = new Dictionary<string, string?>(request.UserIds.Count);
        foreach (var userId in request.UserIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(userId)) continue;
            var profile = await repository.GetAsync(currentUser.TenantId, userId, cancellationToken);
            names[userId] = profile?.DisplayName;
        }

        return Ok(new DisplayNamesResponse(names));
    }

    /// <summary>
    /// Returns an HR-safe summary for a single requestor. Used by Parking Requests
    /// detail panel. Restricted to HR and admin roles; tenant comes from authenticated
    /// context. Returns 404 when no profile exists so the UI can render an explicit
    /// "profile not available" state instead of a silent fallback.
    /// </summary>
    [HttpGet("requestors/{userId}")]
    [ProducesResponseType(typeof(RequestorSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(RequestorSummaryNotFound), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestorSummary(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("userId is required.");

        var profile = await repository.GetAsync(currentUser.TenantId, userId, cancellationToken);
        if (profile is null)
            return NotFound(new RequestorSummaryNotFound(userId, BuildShortRef(userId)));

        var activeVehicles = profile.ActiveVehicles;
        var defaultVehicle = activeVehicles.FirstOrDefault(v => v.IsDefault) ?? activeVehicles.FirstOrDefault();

        return Ok(new RequestorSummaryResponse(
            UserId: userId,
            ShortRef: BuildShortRef(userId),
            DisplayName: profile.DisplayName,
            ProfileStatus: profile.Status.ToString(),
            ParkingEligible: profile.ParkingEligible,
            HasCompanyCar: profile.HasCompanyCar,
            AccessibilityEligible: profile.AccessibilityEligible,
            ReservedSpaceEligible: profile.ReservedSpaceEligible,
            ActiveVehicleCount: activeVehicles.Count,
            DefaultVehicle: defaultVehicle is null ? null : new RequestorVehicleSummary(
                LicensePlate: defaultVehicle.LicensePlate,
                VehicleType: defaultVehicle.VehicleType,
                IsElectric: defaultVehicle.IsElectric,
                IsDefault: defaultVehicle.IsDefault)));
    }

    // Last 6 chars of the userId hash, uppercased — matches the short-ref convention
    // already used as a secondary label on Parking Requests rows.
    private static string BuildShortRef(string userId)
    {
        var clean = userId.Replace("-", string.Empty);
        return clean.Length <= 6 ? clean.ToUpperInvariant() : clean[^6..].ToUpperInvariant();
    }
}

public sealed record DisplayNamesRequest(IReadOnlyList<string>? UserIds);
public sealed record DisplayNamesResponse(IReadOnlyDictionary<string, string?> Names);

public sealed record RequestorSummaryResponse(
    string UserId,
    string ShortRef,
    string? DisplayName,
    string ProfileStatus,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible,
    int ActiveVehicleCount,
    RequestorVehicleSummary? DefaultVehicle);

public sealed record RequestorVehicleSummary(
    string LicensePlate,
    string VehicleType,
    bool IsElectric,
    bool IsDefault);

public sealed record RequestorSummaryNotFound(string UserId, string ShortRef);
