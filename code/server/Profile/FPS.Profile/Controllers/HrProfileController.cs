using FPS.Profile.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

// Class-level [Authorize] only requires authentication. Per-action role
// guards keep the privacy posture sharp: display-name lookup is allowed for
// report_viewer too (issue #474 — HR reports must surface employee names,
// not opaque hashes), but the requestor-summary view stays HR/admin because
// it carries parking eligibility and vehicle facts that are not part of the
// report_viewer surface. Same lesson as MAP001 (#467) — controller-level
// role attributes are additive, so the relaxation has to be per-action.
[ApiController]
[Route("profile/hr")]
[Authorize]
public sealed class HrProfileController(
    IProfileRepository repository,
    ICurrentUser currentUser) : ControllerBase
{
    private const int MaxBatchSize = 200;
    // Issue #482 added auditor: the auditor workspace resolves actor hashes
    // back to user ids and then to names. Without auditor here the workspace
    // silently falls back to short-ref-only labels, missing the "auditor can
    // understand who performed the action" acceptance criterion. Pinned
    // end-to-end by HrProfileAuthorizationTests.DisplayNames_AuditorRole_Returns200.
    private const string HrAndReportingRoles = "hr_manager,admin,report_viewer,auditor";
    private const string HrAdminOnlyRoles = "hr_manager,admin";

    /// <summary>
    /// Returns display names for a batch of subject hashes. Allowed for HR,
    /// admin, report_viewer, AND auditor. Names are never exposed on employee
    /// screens or audit payloads themselves — this endpoint is the seam that
    /// lets the auditor workspace join opaque actor hashes back to people.
    /// </summary>
    [HttpPost("display-names")]
    [Authorize(Roles = HrAndReportingRoles)]
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
    [Authorize(Roles = HrAdminOnlyRoles)]
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
