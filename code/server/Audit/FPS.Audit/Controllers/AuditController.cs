using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Audit.Controllers;

[ApiController]
[Authorize(Roles = $"{AuditRoles.Auditor},{AuditRoles.Admin}")]
public sealed class AuditController(AuditQueryService queryService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/audit")]
    public async Task<IActionResult> Query([FromQuery] AuditQueryRequest query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await queryService.QueryAsync(query, currentUser.TenantId, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Authorize(Roles = $"{AuditRoles.Auditor},{AuditRoles.Admin}")]
public sealed class PiiMappingController(PiiErasureService erasureService, ICurrentUser currentUser) : ControllerBase
{
    [HttpDelete("/audit/pii-mappings/{userId}")]
    public async Task<IActionResult> Delete(string userId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var requestorHash = FPS.Audit.Application.Pseudonymiser.Hash(currentUser.UserId) ?? string.Empty;
        await erasureService.DeleteByUserIdAsync(userId, currentUser.TenantId, requestorHash, cancellationToken);
        return NoContent();
    }
}

internal static class AuditRoles
{
    internal const string Auditor = "auditor";
    internal const string Admin = "admin";
}
