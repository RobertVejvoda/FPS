using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Controllers;

/// <summary>
/// PLAT005A — platform-plane, read-only per-tenant monthly usage statistics. Cross-tenant, so it
/// is restricted to platform roles (platform_admin / platform_operator / platform_auditor — these
/// are <see cref="FPS.SharedKernel.Identity.FpsRoles"/> platform-plane roles). Forged platform_*
/// roles on a customer-issuer token are stripped by the shared TenantClaimsTransformation before
/// authorization, so a tenant/customer token can never reach this endpoint.
///
/// Aggregate counts only — no employee ids, requestor ids, actor hashes, or raw event payloads.
/// Excluded from the open OpenAPI document (and the generated tenant API client) via
/// ApiExplorerSettings.IgnoreApi.
/// </summary>
[ApiController]
[Authorize(Roles = "platform_admin,platform_operator,platform_auditor")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PlatformUsageStatsController(DataHubDbContext db) : ControllerBase
{
    // GET /datahub/platform/usage-stats?month=YYYY-MM[&tenantId=...]
    // Under the /datahub gateway prefix so it is reachable through Envoy (the platform surface
    // for DataHub lives beneath the service's own route, not a separate /platform route).
    [HttpGet("/datahub/platform/usage-stats")]
    public async Task<IActionResult> Get(
        [FromQuery] string month,
        [FromQuery] string? tenantId,
        CancellationToken ct)
    {
        if (!TryParseMonth(month, out var periodMonth))
            return BadRequest(new { error = "month is required in YYYY-MM format, e.g. 2026-06." });

        var query = db.TenantUsageStats.AsNoTracking().Where(u => u.PeriodMonth == periodMonth);
        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(u => u.TenantId == tenantId);

        var rows = await query.OrderBy(u => u.TenantId).ToListAsync(ct);
        return Ok(rows.Select(ToDto).ToList());
    }

    private static bool TryParseMonth(string? month, out DateOnly periodMonth)
    {
        periodMonth = default;
        if (string.IsNullOrWhiteSpace(month)) return false;
        // Accept YYYY-MM (first of month).
        if (DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out var parsed))
        {
            periodMonth = parsed;
            return true;
        }
        return false;
    }

    private static UsageStatsDto ToDto(TenantUsageStatsProjection u) => new(
        u.TenantId,
        u.PeriodMonth.ToString("yyyy-MM"),
        u.ActiveRequestorCount,
        u.BookingRequestCount,
        u.DrawRunCount,
        u.AllocatedCount,
        u.RejectedCount,
        u.CancelledCount,
        u.ExpiredCount,
        u.NoShowCount,
        u.UsedCount,
        u.LastUpdatedAt);
}

/// <summary>Aggregate-only usage statistics for one tenant-month. No PII.</summary>
public sealed record UsageStatsDto(
    string TenantId,
    string Period,
    int ActiveRequestorCount,
    int BookingRequestCount,
    int DrawRunCount,
    int AllocatedCount,
    int RejectedCount,
    int CancelledCount,
    int ExpiredCount,
    int NoShowCount,
    int UsedCount,
    DateTimeOffset LastUpdatedAt);
