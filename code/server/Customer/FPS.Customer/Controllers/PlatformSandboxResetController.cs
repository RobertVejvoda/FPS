using System.Security.Cryptography;
using System.Text;
using FPS.Customer.Application;
using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

/// <summary>
/// PLAT003A/PLAT003B — platform-only manual reset of the evaluation sandbox to its golden state, plus a
/// read-only last-reset evidence surface. Cross-tenant platform-plane operations. The reset (POST) allows
/// platform_operator and platform_admin only; the evidence read (GET) additionally allows platform_auditor
/// (read-only). Tenant admins and employee/customer tokens are rejected on both (forged platform_* roles on
/// a customer token are stripped by TenantClaimsTransformation). The sandbox guard lives in
/// <see cref="SandboxResetService"/> and reads the flag from stored tenant metadata — the caller cannot pass
/// sandbox=true. Excluded from the open OpenAPI client.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PlatformSandboxResetController(
    SandboxResetService reset,
    ISandboxResetEvidenceStore evidence,
    ICurrentUser currentUser) : ControllerBase
{
    // POST /platform/tenants/{tenantId}/reset-sandbox  (follows the PLAT008B /platform/tenants prefix)
    [HttpPost("/platform/tenants/{tenantId}/reset-sandbox")]
    [RequirePlatformOperator]
    public async Task<IActionResult> Reset(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var actorHash = Hash(currentUser.UserId);
        var authorizationHeader = Request.Headers.Authorization.ToString();
        var (summary, error) = await reset.ResetAsync(tenantId, actorHash, source: "manual", authorizationHeader, ct);

        if (error is not null)
            return error.StartsWith("unavailable", StringComparison.OrdinalIgnoreCase)
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { error })
                : error.Contains("Unknown", StringComparison.OrdinalIgnoreCase)
                    ? NotFound(new { error })
                    : BadRequest(new { error });

        return Ok(new
        {
            tenantId = summary!.TenantId,
            purged = summary.Purged,
            profilesSeeded = summary.ProfilesSeeded,
            slotsSeeded = summary.SlotsSeeded,
            completedAt = summary.CompletedAt,
        });
    }

    // GET /platform/tenants/{tenantId}/reset-sandbox — last-reset evidence for platform operators/auditors.
    // No secrets or raw ids: actor hash, status, source, timestamps, snapshot version, aggregate purge counts.
    [HttpGet("/platform/tenants/{tenantId}/reset-sandbox")]
    [RequirePlatformReader]
    public async Task<IActionResult> Status(string tenantId, CancellationToken ct)
    {
        SandboxResetEvidence? latest;
        try { latest = await evidence.GetLatestAsync(tenantId, ct); }
        catch (ArgumentException) { return NotFound(new { error = "Unknown tenant." }); } // invalid id shape
        if (latest is null) return NotFound(new { error = "No reset evidence recorded for this tenant." });

        return Ok(new
        {
            tenantId = latest.TenantId,
            status = latest.Status,
            source = latest.Source,
            startedAt = latest.StartedAt,
            completedAt = latest.CompletedAt,
            snapshotVersion = latest.SnapshotVersion,
            failureReason = latest.FailureReason,
            purged = latest.Purged,
        });
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
