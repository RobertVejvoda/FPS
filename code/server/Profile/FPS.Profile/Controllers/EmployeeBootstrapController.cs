using FPS.Profile.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

[ApiController]
[Authorize(Roles = "admin,hr_manager")]
public sealed class EmployeeBootstrapController(
    EmployeeBootstrapService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("/profile/bootstrap")]
    public async Task<IActionResult> Register([FromBody] BootstrapRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId)) return Unauthorized();

        var req = ToServiceRequest(request, "admin-entry");
        var (profile, error) = await service.RegisterAsync(currentUser.TenantId, req, ct);
        if (error is not null) return BadRequest(new { error });
        return Ok(ToResponse(profile!));
    }

    [HttpPost("/profile/bootstrap/import")]
    public async Task<IActionResult> Import([FromBody] ImportRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId)) return Unauthorized();
        if (request.Employees is null || request.Employees.Count == 0)
            return BadRequest(new { error = "No employees provided." });

        var reqs = request.Employees.Select(e => ToServiceRequest(e, "file-import")).ToList();
        var summary = await service.ImportAsync(currentUser.TenantId, reqs, ct);
        return Ok(summary);
    }

    // UserId from the POST /profile/bootstrap response is used as the path parameter.
    [HttpPut("/profile/bootstrap/{userId}")]
    public async Task<IActionResult> Update(string userId, [FromBody] UpdateRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId)) return Unauthorized();

        var updateReq = new UpdateEmployeeRequest(
            request.IsActive, request.FpsRoles ?? [],
            request.NotificationAddress, request.HomeLocationId,
            request.ParkingEligible, request.HasCompanyCar,
            request.AccessibilityEligible, request.ReservedSpaceEligible);

        var error = await service.UpdateAsync(currentUser.TenantId, userId, updateReq, ct);
        if (error == "Employee not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpPost("/profile/bootstrap/deactivate")]
    public async Task<IActionResult> Deactivate([FromBody] DeactivateRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.ExternalSubject))
            return BadRequest(new { error = "ExternalSubject is required." });

        var error = await service.DeactivateAsync(currentUser.TenantId, request.ExternalSubject, ct);
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    private static BootstrapEmployeeRequest ToServiceRequest(BootstrapRequest r, string factSource) =>
        new(r.ExternalSubject ?? string.Empty, r.EmployeeId, r.IsActive,
            r.FpsRoles ?? [], r.NotificationAddress, r.HomeLocationId,
            r.ParkingEligible, r.HasCompanyCar, r.AccessibilityEligible, r.ReservedSpaceEligible,
            factSource);

    private static object ToResponse(Domain.UserProfile p) => new
    {
        tenantId = p.TenantId,
        userId = p.UserId,
        isActive = p.IsActive,
        parkingEligible = p.ParkingEligible,
        hasCompanyCar = p.HasCompanyCar,
        accessibilityEligible = p.AccessibilityEligible,
        reservedSpaceEligible = p.ReservedSpaceEligible,
        factSource = p.FactSource,
        snapshotVersion = p.SnapshotVersion,
        updatedAt = p.UpdatedAt,
    };
}

public sealed record BootstrapRequest(
    string? ExternalSubject, string? EmployeeId, bool IsActive,
    IReadOnlyList<string>? FpsRoles, string? NotificationAddress, string? HomeLocationId,
    bool ParkingEligible, bool HasCompanyCar, bool AccessibilityEligible, bool ReservedSpaceEligible);

public sealed record ImportRequest(IReadOnlyList<BootstrapRequest>? Employees);
public sealed record DeactivateRequest(string? ExternalSubject);
public sealed record UpdateRequest(
    bool IsActive, IReadOnlyList<string>? FpsRoles,
    string? NotificationAddress, string? HomeLocationId,
    bool ParkingEligible, bool HasCompanyCar, bool AccessibilityEligible, bool ReservedSpaceEligible);
