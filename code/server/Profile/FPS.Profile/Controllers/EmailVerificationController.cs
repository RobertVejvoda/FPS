using FPS.Profile.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

/// <summary>
/// AUTH008 (#729) — email ownership verification for FairSpot-local accounts. The signed-in user requests
/// verification of their account email (a one-time link is delivered out-of-band) and confirms it by
/// presenting the token. Verification proves email ownership only — tenant/user/role access still comes
/// from authenticated claims. The token is Secret: it is accepted in the request body (never a query
/// string) and never returned or logged.
/// </summary>
[ApiController]
[Route("profile/email/verification")]
[Authorize]
public sealed class EmailVerificationController(
    EmailVerificationService service,
    IProfileRepository profiles,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("request")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Request(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        var profile = await profiles.GetAsync(currentUser.TenantId, currentUser.UserId, cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.NotificationAddress))
            return BadRequest("No email address is set for this account.");

        var error = await service.RequestAsync(currentUser.TenantId, currentUser.UserId, profile.NotificationAddress, cancellationToken);
        if (error is not null)
            return BadRequest("The account email address is not valid for verification.");

        // 202 — the verification link is delivered out-of-band; no token is returned in the response.
        return Accepted();
    }

    [HttpPost("confirm")]
    [ProducesResponseType(typeof(EmailVerificationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(EmailVerificationRejectedResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Confirm(
        [FromBody] ConfirmEmailVerificationRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId) || string.IsNullOrEmpty(currentUser.UserId))
            return Unauthorized();

        if (request is null || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new EmailVerificationRejectedResponse("token_required"));

        var outcome = await service.ConfirmAsync(currentUser.TenantId, currentUser.UserId, request.Token, cancellationToken);
        return outcome.Verified
            ? Ok(new EmailVerificationStatusResponse(true))
            : BadRequest(new EmailVerificationRejectedResponse(outcome.RejectionReason ?? "verification_failed"));
    }
}

public sealed record ConfirmEmailVerificationRequest(string Token);
public sealed record EmailVerificationStatusResponse(bool Verified);
public sealed record EmailVerificationRejectedResponse(string Reason);
