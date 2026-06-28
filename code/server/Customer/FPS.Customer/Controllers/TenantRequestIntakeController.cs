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
    [ProducesResponseType(typeof(TenantRequestAcknowledgement), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

/// <summary>Intake rate-limit policy name + trusted client-IP resolution (used by Program).</summary>
public static class TenantRequestRateLimit
{
    public const string PolicyName = "tenant-request-intake";

    /// <summary>
    /// Cloudflare's authoritative client-IP header. FairSpot's public boundary is reachable only
    /// through Cloudflare Tunnel (see docs/production/nas-cloudflare-deployment-profile.md and the
    /// WAF profile), and Cloudflare sets — and overwrites — this header with the true client IP,
    /// so it is the trusted client identifier here.
    /// </summary>
    public const string CloudflareClientIpHeader = "CF-Connecting-IP";

    /// <summary>
    /// Partition key for the public intake limiter: the real client IP, so the window is per-client
    /// rather than a single global bucket behind the proxy. We trust <see cref="CloudflareClientIpHeader"/>
    /// (set only by the Cloudflare edge) — deliberately <b>not</b> arbitrary <c>X-Forwarded-For</c>.
    /// Without Cloudflare (local/dev) the socket peer address is the client, so we fall back to it.
    /// </summary>
    public static string ClientPartitionKey(string? cloudflareClientIp, System.Net.IPAddress? remoteIp)
    {
        if (!string.IsNullOrWhiteSpace(cloudflareClientIp) &&
            System.Net.IPAddress.TryParse(cloudflareClientIp.Trim(), out var parsed))
            return parsed.ToString();

        return remoteIp?.ToString() ?? "unknown";
    }
}
