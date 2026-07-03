using FPS.DataHub.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Controllers;

/// <summary>
/// PLAT008E — platform-plane, read-only Draw health for the operator health strip. Cross-tenant, so
/// restricted to platform roles (mirrors <see cref="PlatformUsageStatsController"/>). Forged
/// platform_* roles on a customer-issuer token are stripped by the shared TenantClaimsTransformation
/// before authorization, so a tenant/customer token can never reach this endpoint.
///
/// Aggregate-only, operator-safe: counts and a couple of timestamps. No tenant ids, location ids,
/// draw attempt ids, actor ids, or raw failure text — those stay in the tenant-scoped HR views.
/// Excluded from the open OpenAPI document (ApiExplorerSettings.IgnoreApi).
/// </summary>
[ApiController]
[Authorize(Roles = "platform_admin,platform_operator,platform_auditor")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PlatformDrawHealthController(DataHubDbContext db) : ControllerBase
{
    // A draw is "stuck" when it started but never completed or failed within this window. Normal
    // draws finish in seconds; anything Running for over half an hour is a red flag worth surfacing.
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(30);

    // GET /datahub/platform/draw-health?windowDays=7
    [HttpGet("/datahub/platform/draw-health")]
    public async Task<IActionResult> Get([FromQuery] int windowDays = 7, CancellationToken ct = default)
    {
        var days = windowDays is >= 1 and <= 90 ? windowDays : 7;
        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-days).UtcDateTime;
        var stuckBefore = now.Subtract(StuckThreshold).UtcDateTime;

        // Consider draws that saw activity within the window (projection freshness is LastUpdatedAt).
        var recent = db.DrawHistory.AsNoTracking().Where(d => d.LastUpdatedAt >= since);

        var completedCount = await recent.CountAsync(d => d.Status == "Completed", ct);
        var failedCount = await recent.CountAsync(d => d.Status == "Failed", ct);
        var runningCount = await recent.CountAsync(d => d.Status == "Running", ct);
        // Stuck: still Running (never completed/failed) and started long enough ago to be abnormal.
        var stuckCount = await recent.CountAsync(
            d => d.Status == "Running" && d.CompletedAt == null && d.StartedAt != null && d.StartedAt < stuckBefore, ct);

        var lastFailureAt = await db.DrawHistory.AsNoTracking()
            .Where(d => d.Status == "Failed" && d.CompletedAt != null)
            .OrderByDescending(d => d.CompletedAt)
            .Select(d => (DateTime?)d.CompletedAt)
            .FirstOrDefaultAsync(ct);

        var lastActivityAt = await db.DrawHistory.AsNoTracking()
            .OrderByDescending(d => d.LastUpdatedAt)
            .Select(d => (DateTimeOffset?)d.LastUpdatedAt)
            .FirstOrDefaultAsync(ct);

        return Ok(new DrawHealthDto(
            days, completedCount, failedCount, runningCount, stuckCount, lastFailureAt, lastActivityAt));
    }
}

/// <summary>Aggregate-only Draw health over a recent window. No PII, ids, or raw failure text.</summary>
public sealed record DrawHealthDto(
    int WindowDays,
    int CompletedCount,
    int FailedCount,
    int RunningCount,
    int StuckCount,
    DateTime? LastFailureAt,
    DateTimeOffset? LastActivityAt);
