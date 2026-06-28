using FPS.Customer.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FPS.Customer.Controllers;

/// <summary>
/// PLAT004 — public, unauthenticated "Request a tenant" intake. Turnstile + a per-IP rate limit
/// guard the open path; a successful submission records a TenantRequest and alerts sales. No
/// tenant is provisioned. The platform-operator triage queue is a separate, platform-gated surface.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class TenantRequestIntakeController(TenantRequestService service) : ControllerBase
{
    [HttpPost("/tenant-requests")]
    [EnableRateLimiting(TenantRequestRateLimit.PolicyName)]
    public async Task<IActionResult> Submit([FromBody] SubmitTenantRequest body, CancellationToken ct)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (request, error) = await service.SubmitAsync(
            body.Company, body.PrimaryDomain, body.ContactEmail, body.Message,
            body.TurnstileToken, remoteIp, ct);

        if (error is not null) return BadRequest(new { error });

        // Acknowledge without echoing prospect PII back to the open path.
        return Accepted(new TenantRequestAcknowledgement(request!.RequestId, request.Status.ToString()));
    }
}

public sealed record SubmitTenantRequest(
    string? Company, string? PrimaryDomain, string? ContactEmail, string? Message, string? TurnstileToken);

public sealed record TenantRequestAcknowledgement(string RequestId, string Status);

/// <summary>Shared name for the intake rate-limit policy (registered in Program).</summary>
public static class TenantRequestRateLimit
{
    public const string PolicyName = "tenant-request-intake";
}
