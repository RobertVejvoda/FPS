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

[ApiController]
[Authorize(Roles = $"{AuditRoles.Admin}")]
public sealed class AuditRetentionController(
    AuditRetentionService retentionService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("/audit/retention")]
    [ProducesResponseType(typeof(RetentionExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Execute(
        [FromBody] AuditRetentionRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var retentionDays = request.RetentionDays ?? AuditRetentionPolicy.DefaultRetentionDays;
        if (retentionDays < 1)
            return BadRequest("RetentionDays must be at least 1.");

        var policy = new AuditRetentionPolicy(currentUser.TenantId, retentionDays);
        var result = await retentionService.ExecuteAsync(policy, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Authorize(Roles = $"{AuditRoles.Auditor},{AuditRoles.Admin}")]
public sealed class AuditIntegrityController(
    AuditIntegrityService integrityService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/audit/integrity")]
    [ProducesResponseType(typeof(IntegrityVerificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Verify(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? expectedHash,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await integrityService.VerifyAsync(currentUser.TenantId, from, to, expectedHash, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/audit/export")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditExportRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var records = await integrityService.ExportAsync(currentUser.TenantId, from, to, cancellationToken);
        return Ok(records);
    }
}

public sealed record AuditRetentionRequest(int? RetentionDays);

internal static class AuditRoles
{
    internal const string Auditor = "auditor";
    internal const string Admin = "admin";
}
