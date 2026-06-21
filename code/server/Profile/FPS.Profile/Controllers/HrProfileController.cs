using FPS.Profile.Application;
using FPS.Profile.Domain;
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
    EmployeeBootstrapService bootstrapService,
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

    /// <summary>
    /// Update allocation-impacting eligibility flags (company car, accessibility)
    /// for a single requestor. HR/admin only. Issue #481: these flags must not
    /// be self-service for employees because they influence draw priority and
    /// slot eligibility; the existing employee endpoints (/profile/vehicles,
    /// /profile/snapshot) never expose them as writable. A null field on the
    /// request body leaves that flag untouched, so the caller can flip one
    /// without round-tripping the other.
    ///
    /// AUDIT GAP: Profile service does not currently emit domain events, so
    /// this change is not surfaced in the audit timeline. Adding event
    /// publication is out of scope for this slice and should follow the
    /// booking-event pattern once the Profile bus binding lands.
    /// </summary>
    [HttpPatch("requestors/{userId}/eligibility")]
    [Authorize(Roles = HrAdminOnlyRoles)]
    [ProducesResponseType(typeof(EligibilityUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEligibility(
        string userId,
        [FromBody] EligibilityUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("userId is required.");

        var (profile, error) = await bootstrapService.UpdateEligibilityAsync(
            currentUser.TenantId, userId,
            request.HasCompanyCar, request.AccessibilityEligible, cancellationToken);

        if (error == "Employee not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });

        return Ok(new EligibilityUpdateResponse(
            UserId: profile!.UserId,
            ShortRef: BuildShortRef(profile.UserId),
            HasCompanyCar: profile.HasCompanyCar,
            AccessibilityEligible: profile.AccessibilityEligible,
            SnapshotVersion: profile.SnapshotVersion,
            UpdatedAt: profile.UpdatedAt));
    }

    // Last 6 chars of the userId hash, uppercased — matches the short-ref convention
    // already used as a secondary label on Parking Requests rows.
    private static string BuildShortRef(string userId)
    {
        var clean = userId.Replace("-", string.Empty);
        return clean.Length <= 6 ? clean.ToUpperInvariant() : clean[^6..].ToUpperInvariant();
    }

    /// <summary>
    /// Returns per-location company-car employee counts for the tenant, so the
    /// Configuration UI can compare against active compatible fixed company-car
    /// slot capacity and warn HR/admin when entitlements exceed capacity.
    ///
    /// Issue #533: only counts ACTIVE profiles with HasCompanyCar = true. Each
    /// row carries the count of employees whose HomeLocationId matches the
    /// location, plus the distinct user ids so the UI can subtract guaranteed
    /// users (those reserved on an active company-car-only slot) from the count
    /// and surface the residual as "without guaranteed slot". Employees with no
    /// HomeLocationId are reported under the synthetic "unassigned" bucket so
    /// HR sees them but they do not inflate any specific location's warning.
    ///
    /// PRIVACY: the response only contains userId hashes already known to the
    /// HR/admin role (HR can already look up requestor summaries by hash);
    /// names and notification addresses are NOT included.
    /// </summary>
    [HttpGet("company-car-locations")]
    [Authorize(Roles = HrAdminOnlyRoles)]
    [ProducesResponseType(typeof(CompanyCarLocationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCompanyCarLocationSummary(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var profiles = await repository.ListByTenantAsync(currentUser.TenantId, cancellationToken);

        // Group active company-car employees by HomeLocationId. The synthetic
        // null bucket surfaces as locationId="" so HR can still see the count.
        // Each employee is projected with the vehicle/accessibility traits the
        // Configuration warning needs to mirror AvailableSlot.CanAccommodate:
        // requiresChargerForEveryRequest is true only when EVERY active
        // vehicle is electric (any ICE option lets the employee request
        // without a charger). requiresAccessibleSpot reflects the profile
        // flag the allocator promotes to VehicleInformation at request time.
        // User ids are deduplicated case-insensitively to match
        // AvailableSlot.IsReservedFor semantics.
        var grouped = profiles
            .Where(p => p.HasCompanyCar && p.IsActive)
            .GroupBy(p => p.HomeLocationId ?? string.Empty, StringComparer.Ordinal)
            .Select(g => new CompanyCarLocationRow(
                LocationId: g.Key,
                CompanyCarEmployeeCount: g
                    .Select(p => p.UserId)
                    .Where(uid => !string.IsNullOrWhiteSpace(uid))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                CompanyCarUsers: g
                    .Where(p => !string.IsNullOrWhiteSpace(p.UserId))
                    .GroupBy(p => p.UserId, StringComparer.OrdinalIgnoreCase)
                    .Select(byUser => BuildCompanyCarUser(byUser.First()))
                    .OrderBy(u => u.UserId, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderBy(r => r.LocationId, StringComparer.Ordinal)
            .ToList();

        return Ok(new CompanyCarLocationSummaryResponse(grouped));
    }

    private static CompanyCarUserRow BuildCompanyCarUser(Domain.UserProfile profile)
    {
        var activeVehicles = profile.ActiveVehicles;
        // "Requires a charger on EVERY request" only when the employee has at
        // least one active vehicle AND all of them are electric. If the
        // profile has any ICE option, the employee can request without the
        // charger constraint, so we must not flag the slot as incompatible.
        var requiresChargerForEveryRequest =
            activeVehicles.Count > 0 && activeVehicles.All(v => v.IsElectric);
        return new CompanyCarUserRow(
            UserId: profile.UserId,
            RequiresChargerForEveryRequest: requiresChargerForEveryRequest,
            RequiresAccessibleSpot: profile.AccessibilityEligible);
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

public sealed record EligibilityUpdateRequest(
    bool? HasCompanyCar,
    bool? AccessibilityEligible);

public sealed record EligibilityUpdateResponse(
    string UserId,
    string ShortRef,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    string SnapshotVersion,
    DateTimeOffset UpdatedAt);

public sealed record RequestorSummaryNotFound(string UserId, string ShortRef);

// Issue #533: per-location company-car summary used by the Configuration UI
// to detect when entitlements exceed active compatible fixed company-car
// capacity. The Configuration UI joins this with slot data to compute the
// warning client-side, keeping Profile and Configuration services decoupled.
// Each user row carries the vehicle/accessibility traits the warning needs
// to mirror AvailableSlot.CanAccommodate so the UI does not have to refetch
// individual profiles.
public sealed record CompanyCarLocationRow(
    string LocationId,
    int CompanyCarEmployeeCount,
    IReadOnlyList<CompanyCarUserRow> CompanyCarUsers);

public sealed record CompanyCarUserRow(
    string UserId,
    bool RequiresChargerForEveryRequest,
    bool RequiresAccessibleSpot);

public sealed record CompanyCarLocationSummaryResponse(
    IReadOnlyList<CompanyCarLocationRow> Locations);
