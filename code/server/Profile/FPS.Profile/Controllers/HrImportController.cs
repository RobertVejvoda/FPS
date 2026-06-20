using FPS.Profile.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

[ApiController]
[Authorize(Roles = "admin,hr_manager")]
public sealed class HrImportController(
    HrImportService importService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("/profile/admin/hr-import/preview")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB (employees + optional vehicles)
    public async Task<IActionResult> Preview(IFormFile employees, IFormFile? vehicles, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId)) return Unauthorized();
        if (employees is null || employees.Length == 0) return BadRequest(new { error = "employees CSV file is required." });

        await using var empStream = employees.OpenReadStream();
        Stream? vehicleStream = vehicles is { Length: > 0 } ? vehicles.OpenReadStream() : null;
        try
        {
            var (preview, error) = await importService.PreviewAsync(currentUser.TenantId, empStream, vehicleStream, ct);
            if (error is not null) return BadRequest(new { error });
            return Ok(preview);
        }
        finally
        {
            if (vehicleStream is not null) await vehicleStream.DisposeAsync();
        }
    }

    [HttpPost("/profile/admin/hr-import/commit")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Commit(IFormFile employees, IFormFile? vehicles, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId)) return Unauthorized();
        if (employees is null || employees.Length == 0) return BadRequest(new { error = "employees CSV file is required." });

        await using var empStream = employees.OpenReadStream();
        Stream? vehicleStream = vehicles is { Length: > 0 } ? vehicles.OpenReadStream() : null;
        try
        {
            var (result, error) = await importService.CommitAsync(currentUser.TenantId, empStream, vehicleStream, ct);
            if (error is not null) return BadRequest(new { error });
            return Ok(result);
        }
        finally
        {
            if (vehicleStream is not null) await vehicleStream.DisposeAsync();
        }
    }
}
