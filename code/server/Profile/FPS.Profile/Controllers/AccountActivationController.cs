using System.Threading.RateLimiting;
using FPS.Profile.Application;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FPS.Profile.Controllers;

/// <summary>
/// AUTH009 (#738) — pending-account activation gate. An admin/provisioning caller issues (or revokes) a
/// one-time activation challenge for an existing inactive user in their own tenant (tenant comes from the
/// admin's claims, never the body). The invited user activates through the <b>anonymous</b> confirm path
/// by presenting the opaque challenge id + token from the emailed link — the confirm side trusts no
/// caller-supplied tenant/user/role/email; (tenant, user) are resolved from the stored challenge. The
/// token is Secret and never returned or logged. Separate from AUTH008B <c>profile/email/verification</c>.
/// </summary>
[ApiController]
[Route("profile/account-activation")]
public sealed class AccountActivationController(
    AccountActivationService service,
    ICurrentUser currentUser) : ControllerBase
{
    // Admin/provisioning path — issues or refreshes the activation challenge for a pending user.
    [HttpPost("issue")]
    [Authorize(Roles = "admin,hr_manager")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Issue(
        [FromBody] IssueActivationRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new ActivationRejectedResponse("user_id_required"));

        // Tenant is the admin's own tenant — never taken from the request body.
        var result = await service.IssueAsync(currentUser.TenantId, request.UserId, cancellationToken);
        return result.Issued
            ? Accepted() // the activation link is delivered out-of-band; no token/challenge is returned
            : BadRequest(new ActivationRejectedResponse(result.RejectionReason ?? "issue_failed"));
    }

    // Admin/provisioning path — revokes a pending activation challenge.
    [HttpPost("revoke")]
    [Authorize(Roles = "admin,hr_manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(
        [FromBody] RevokeActivationRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new ActivationRejectedResponse("user_id_required"));

        var revoked = await service.RevokeAsync(currentUser.TenantId, request.UserId, cancellationToken);
        return revoked ? Ok(new ActivationStatusResponse(false)) : NotFound();
    }

    // Anonymous activation confirm — the token + challenge id ARE the proof of access. No claims are
    // required or trusted; a per-IP rate limit bounds guessing/abuse in addition to the per-challenge
    // attempt limit. The token is accepted only in the request body, never the query string.
    [HttpPost("confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(AccountActivationRateLimit.PolicyName)]
    [ProducesResponseType(typeof(ActivationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ActivationRejectedResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(
        [FromBody] ConfirmActivationRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ChallengeId) || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new ActivationRejectedResponse("invalid_request"));

        var outcome = await service.ConfirmAsync(request.ChallengeId, request.Token, cancellationToken);
        return outcome.Activated
            ? Ok(new ActivationStatusResponse(true))
            : BadRequest(new ActivationRejectedResponse(outcome.RejectionReason ?? "activation_failed"));
    }
}

public sealed record IssueActivationRequest(string UserId);
public sealed record RevokeActivationRequest(string UserId);
public sealed record ConfirmActivationRequest(string ChallengeId, string Token);
public sealed record ActivationStatusResponse(bool Activated);
public sealed record ActivationRejectedResponse(string Reason);

/// <summary>AUTH009 anonymous-confirm rate-limit policy + trusted client-IP partitioning (used by Program).</summary>
public static class AccountActivationRateLimit
{
    public const string PolicyName = "account-activation-confirm";

    // Behind Cloudflare the real client IP is in CF-Connecting-IP; fall back to the socket remote IP.
    public const string CloudflareClientIpHeader = "CF-Connecting-IP";

    public static string ClientPartitionKey(string? cloudflareIp, string? remoteIp) =>
        !string.IsNullOrWhiteSpace(cloudflareIp) ? cloudflareIp!
        : !string.IsNullOrWhiteSpace(remoteIp) ? remoteIp!
        : "unknown";

    public static RateLimitPartition<string> Partition(HttpContext httpContext)
    {
        var key = ClientPartitionKey(
            httpContext.Request.Headers[CloudflareClientIpHeader].FirstOrDefault(),
            httpContext.Connection.RemoteIpAddress?.ToString());
        return RateLimitPartition.GetFixedWindowLimiter(
            key, _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 });
    }
}
