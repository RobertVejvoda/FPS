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

        var all = db.DrawHistory.AsNoTracking();

        // Whether the projection has ANY draw evidence at all. When false, draw health cannot be
        // proven, and the strip shows "not wired" rather than a false green.
        var hasEvidence = await all.AnyAsync(ct);

        // Recent outcome counts (projection freshness is LastUpdatedAt).
        var recent = all.Where(d => d.LastUpdatedAt >= since);
        var completedCount = await recent.CountAsync(d => d.Status == "Completed", ct);
        var failedCount = await recent.CountAsync(d => d.Status == "Failed", ct);

        // Running / stuck are NOT window-filtered: a draw that started but never completed is a red
        // flag no matter how long ago — filtering it out by the recent window would hide it.
        var runningCount = await all.CountAsync(d => d.Status == "Running" && d.CompletedAt == null, ct);
        var stuckCount = await all.CountAsync(
            d => d.Status == "Running" && d.CompletedAt == null && d.StartedAt != null && d.StartedAt < stuckBefore, ct);

        var lastFailureAt = await all
            .Where(d => d.Status == "Failed" && d.CompletedAt != null)
            .OrderByDescending(d => d.CompletedAt)
            .Select(d => (DateTime?)d.CompletedAt)
            .FirstOrDefaultAsync(ct);

        var lastActivityAt = await all
            .OrderByDescending(d => d.LastUpdatedAt)
            .Select(d => (DateTimeOffset?)d.LastUpdatedAt)
            .FirstOrDefaultAsync(ct);

        // Stale: evidence exists but nothing has updated within the window, so freshness cannot be
        // shown as healthy — the strip surfaces it instead of a green OK.
        var stale = hasEvidence && (lastActivityAt is null || lastActivityAt.Value.UtcDateTime < since);

        return Ok(new DrawHealthDto(
            days, hasEvidence, stale, completedCount, failedCount, runningCount, stuckCount, lastFailureAt, lastActivityAt));
    }
}

/// <summary>
/// Aggregate-only Draw health over a recent window. No PII, ids, or raw failure text.
/// HasEvidence=false → no draw projection rows at all (health can't be proven → not wired).
/// Stale=true → evidence exists but nothing updated within the window (freshness can't be proven).
/// Neither state may render as a healthy green.
/// </summary>
public sealed record DrawHealthDto(
    int WindowDays,
    bool HasEvidence,
    bool Stale,
    int CompletedCount,
    int FailedCount,
    int RunningCount,
    int StuckCount,
    DateTime? LastFailureAt,
    DateTimeOffset? LastActivityAt);
