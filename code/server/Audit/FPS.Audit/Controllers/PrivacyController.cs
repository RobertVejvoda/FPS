using FPS.Audit.Application.Privacy;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Audit.Controllers;

[ApiController]
[Authorize(Roles = "admin,auditor")]
public sealed class PrivacyController(PrivacyService privacyService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("/privacy/erasure-requests")]
    [ProducesResponseType(typeof(ErasureStatusResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateErasureRequest(
        [FromBody] CreateErasureRequest body,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.TargetUserId))
            return BadRequest("TargetUserId is required.");
        if (string.IsNullOrWhiteSpace(body.LegalBasis))
            return BadRequest("LegalBasis is required.");

        var request = await privacyService.CreateErasureRequestAsync(
            currentUser.TenantId, body.TargetUserId, currentUser.UserId ?? string.Empty,
            body.LegalBasis, cancellationToken);

        var response = new ErasureStatusResponse(
            request.ErasureRequestId, request.TenantId, request.TargetActorHash,
            request.RequestedByActorHash, request.LegalBasis, request.RequestedAt,
            request.Status, request.CompletedAt, request.ServiceResults, request.BlockReason);

        return Accepted($"/privacy/erasure-requests/{request.ErasureRequestId}", response);
    }

    [HttpGet("/privacy/erasure-requests/{erasureRequestId}")]
    [ProducesResponseType(typeof(ErasureStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string erasureRequestId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var status = await privacyService.GetStatusAsync(erasureRequestId, currentUser.TenantId, cancellationToken);
        if (status is null) return NotFound();

        return Ok(status);
    }
}
