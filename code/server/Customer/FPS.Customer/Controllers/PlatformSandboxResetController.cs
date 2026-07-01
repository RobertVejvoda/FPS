using System.Security.Cryptography;
using System.Text;
using FPS.Customer.Application;
using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

/// <summary>
/// PLAT003A — platform-only manual reset of the evaluation sandbox to its golden state. Cross-tenant
/// platform-plane operation: <see cref="RequirePlatformOperatorAttribute"/> allows platform_operator
/// and platform_admin only; platform_auditor, tenant admins, and employee/customer tokens are
/// rejected (forged platform_* roles on a customer token are stripped by TenantClaimsTransformation).
/// The sandbox guard lives in <see cref="SandboxResetService"/> and reads the flag from stored
/// tenant metadata — the caller cannot pass sandbox=true. Excluded from the open OpenAPI client.
/// </summary>
[ApiController]
[RequirePlatformOperator]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PlatformSandboxResetController(SandboxResetService reset, ICurrentUser currentUser) : ControllerBase
{
    // POST /platform/tenants/{tenantId}/reset-sandbox  (follows the PLAT008B /platform/tenants prefix)
    [HttpPost("/platform/tenants/{tenantId}/reset-sandbox")]
    public async Task<IActionResult> Reset(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var actorHash = Hash(currentUser.UserId);
        var authorizationHeader = Request.Headers.Authorization.ToString();
        var (summary, error) = await reset.ResetAsync(tenantId, actorHash, authorizationHeader, ct);

        if (error is not null)
            return error.Contains("Unknown", StringComparison.OrdinalIgnoreCase)
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

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
