using FPS.Customer.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

[ApiController]
[Authorize(Roles = "admin,hr_manager")]
public sealed class EmployeeBootstrapController(
    EmployeeBootstrapService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("/tenants/{tenantId}/employee-bootstrap")]
    public async Task<IActionResult> Register(
        string tenantId, [FromBody] EmployeeBootstrapRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var actorHash = EmployeeBootstrapService.Hash(currentUser.UserId);
        var req = ToServiceRequest(request, "admin-entry");
        var (record, error) = await service.RegisterAsync(tenantId, req, actorHash, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return Ok(ToResponse(record!));
    }

    [HttpPost("/tenants/{tenantId}/employee-bootstrap/import")]
    public async Task<IActionResult> Import(
        string tenantId, [FromBody] ImportRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();
        if (request.Employees is null || request.Employees.Count == 0)
            return BadRequest(new { error = "No employees provided." });

        var actorHash = EmployeeBootstrapService.Hash(currentUser.UserId);
        var reqs = request.Employees.Select(e => ToServiceRequest(e, "file-import")).ToList();
        var summary = await service.ImportAsync(tenantId, reqs, actorHash, ct);
        return Ok(summary);
    }

    [HttpPut("/tenants/{tenantId}/employee-bootstrap/{subjectHash}")]
    public async Task<IActionResult> Update(
        string tenantId, string subjectHash, [FromBody] UpdateBootstrapRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var actorHash = EmployeeBootstrapService.Hash(currentUser.UserId);
        var updateReq = new UpdateEmployeeRequest(
            request.IsActive, request.FpsRoles ?? [], request.NotificationAddress,
            request.HomeLocationId, request.ParkingEligible,
            request.HasCompanyCar, request.AccessibilityEligible, request.ReservedSpaceEligible);

        var error = await service.UpdateAsync(tenantId, subjectHash, updateReq, actorHash, ct);
        if (error == "Tenant not found." || error == "Employee not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpPost("/tenants/{tenantId}/employee-bootstrap/deactivate")]
    public async Task<IActionResult> Deactivate(
        string tenantId, [FromBody] DeactivateRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.ExternalSubject))
            return BadRequest(new { error = "ExternalSubject is required." });

        var actorHash = EmployeeBootstrapService.Hash(currentUser.UserId);
        var error = await service.DeactivateAsync(tenantId, request.ExternalSubject, actorHash, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpGet("/tenants/{tenantId}/employee-bootstrap/summary")]
    public async Task<IActionResult> GetSummary(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();
        var summary = await service.GetSummaryAsync(tenantId, ct);
        return Ok(summary);
    }

    private static BootstrapEmployeeRequest ToServiceRequest(EmployeeBootstrapRequest r, string factSource) =>
        new(r.ExternalSubject ?? string.Empty, r.EmployeeId, r.IsActive,
            r.FpsRoles ?? [], r.NotificationAddress, r.HomeLocationId,
            r.ParkingEligible, r.HasCompanyCar, r.AccessibilityEligible, r.ReservedSpaceEligible,
            factSource);

    private static object ToResponse(Domain.EmployeeBootstrapRecord r) => new
    {
        tenantId = r.TenantId,
        externalSubjectHash = r.ExternalSubjectHash,
        employeeId = r.EmployeeId,
        isActive = r.IsActive,
        fpsRoles = r.FpsRoles,
        notificationAddress = r.NotificationAddress,
        homeLocationId = r.HomeLocationId,
        parkingEligible = r.ParkingEligible,
        hasCompanyCar = r.HasCompanyCar,
        accessibilityEligible = r.AccessibilityEligible,
        reservedSpaceEligible = r.ReservedSpaceEligible,
        factSource = r.FactSource,
        recordedAt = r.RecordedAt,
        updatedAt = r.UpdatedAt,
    };
}

public sealed record EmployeeBootstrapRequest(
    string? ExternalSubject,
    string? EmployeeId,
    bool IsActive,
    IReadOnlyList<string>? FpsRoles,
    string? NotificationAddress,
    string? HomeLocationId,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible);

public sealed record ImportRequest(IReadOnlyList<EmployeeBootstrapRequest>? Employees);

public sealed record DeactivateRequest(string? ExternalSubject);

public sealed record UpdateBootstrapRequest(
    bool IsActive,
    IReadOnlyList<string>? FpsRoles,
    string? NotificationAddress,
    string? HomeLocationId,
    bool ParkingEligible,
    bool HasCompanyCar,
    bool AccessibilityEligible,
    bool ReservedSpaceEligible);
