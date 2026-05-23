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
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> Preview(IFormFile employees, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId)) return Unauthorized();
        if (employees is null || employees.Length == 0) return BadRequest(new { error = "employees CSV file is required." });

        await using var stream = employees.OpenReadStream();
        var (preview, error) = await importService.PreviewAsync(currentUser.TenantId, stream, ct);
        if (error is not null) return BadRequest(new { error });
        return Ok(preview);
    }

    [HttpPost("/profile/admin/hr-import/commit")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Commit(IFormFile employees, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId)) return Unauthorized();
        if (employees is null || employees.Length == 0) return BadRequest(new { error = "employees CSV file is required." });

        await using var stream = employees.OpenReadStream();
        var (result, error) = await importService.CommitAsync(currentUser.TenantId, stream, ct);
        if (error is not null) return BadRequest(new { error });
        return Ok(result);
    }
}
