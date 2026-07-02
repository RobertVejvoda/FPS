using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

/// <summary>
/// PLAT008B — read-only, cross-tenant platform tenant directory + detail. Platform-plane only
/// (<see cref="RequirePlatformReaderAttribute"/>: platform_admin / operator / auditor); a
/// tenant/customer token can never reach it, and authorization never uses tenant claims.
///
/// Excluded from the open OpenAPI/@fps/api-client surface (ApiExplorerSettings.IgnoreApi) — this
/// is a platform-plane surface that will move to the private fairspot-platform repo (#675). The
/// platform web calls it directly. No mutating tenant actions live here.
/// </summary>
[ApiController]
[RequirePlatformReader]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PlatformTenantsController(
    TenantService tenantService,
    TenantReadinessService readinessService,
    TenantIdentityService identityService) : ControllerBase
{
    [HttpGet("/platform/tenants")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenants = await tenantService.ListAsync(ct);
        return Ok(tenants.Select(ToRow).ToList());
    }

    [HttpGet("/platform/tenants/{tenantId}")]
    public async Task<IActionResult> Detail(string tenantId, CancellationToken ct)
    {
        TenantWorkspace? tenant;
        try { tenant = await tenantService.GetAsync(tenantId, ct); }
        catch (ArgumentException) { return NotFound(); }
        if (tenant is null) return NotFound();

        // Readiness is read as a dry run so the detail view never mutates tenant state.
        var (report, _) = await readinessService.CheckAsync(tenantId, dryRun: true, ct);
        var identity = await identityService.GetConfigAsync(tenantId, ct);

        return Ok(new PlatformTenantDetail(
            ToRow(tenant),
            tenant.SupportContacts,
            tenant.Branding.LoginMode.ToString(),
            tenant.DiscoveryDomains.Select(d => d.Domain).ToList(),
            report is null ? null : new PlatformReadiness(
                report.IsReady,
                report.Checks.Select(c => new PlatformReadinessCheck(c.Name, c.Status.ToString(), string.IsNullOrEmpty(c.Reason) ? null : c.Reason)).ToList()),
            identity is null ? null : new PlatformIdentity(
                identity.TrustedIssuer, identity.Audience, identity.RoleClaimNames, identity.RoleMapping, identity.LocalAccountPolicyEnabled),
            tenant.Transitions.Select(t => new PlatformTransition(t.From.ToString(), t.To.ToString(), t.ActorId, t.OccurredAt, t.Reason)).ToList()));
    }

    private static PlatformTenantRow ToRow(TenantWorkspace t) => new(
        t.TenantId, t.Slug, t.DisplayName, t.Region, t.TimeZone,
        t.Kind.ToString(), t.LifecycleState.ToString(),
        t.PrimaryModule.ToString(), t.EnabledModules.Select(m => m.ToString()).ToList(),
        t.CreatedAt, t.UpdatedAt);
}

public sealed record PlatformTenantRow(
    string TenantId, string Slug, string DisplayName, string Region, string TimeZone,
    string Kind, string LifecycleState,
    // PLAT007B — primary module and all enabled modules (primary first) for operator visibility.
    string PrimaryModule, IReadOnlyList<string> EnabledModules,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record PlatformReadinessCheck(string Name, string Status, string? Reason);
public sealed record PlatformReadiness(bool IsReady, IReadOnlyList<PlatformReadinessCheck> Checks);
public sealed record PlatformIdentity(
    string TrustedIssuer, string Audience, IReadOnlyList<string> RoleClaimNames,
    IReadOnlyDictionary<string, string> RoleMapping, bool LocalAccountPolicyEnabled);
public sealed record PlatformTransition(string From, string To, string ActorId, DateTimeOffset OccurredAt, string? Reason);

public sealed record PlatformTenantDetail(
    PlatformTenantRow Overview,
    IReadOnlyList<TenantSupportContact> SupportContacts,
    string LoginMode,
    IReadOnlyList<string> DiscoveryDomains,
    PlatformReadiness? Readiness,
    PlatformIdentity? Identity,
    IReadOnlyList<PlatformTransition> LifecycleHistory);
