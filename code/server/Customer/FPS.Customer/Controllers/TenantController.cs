using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

// Every /tenants/{tenantId} operation here is tenant-scoped self-administration
// (RequireTenantAdmin = platform_admin cross-tenant, or the tenant's own admin). Creating a
// tenant is a platform-plane operator operation and lives on TenantProvisioningController.
[ApiController]
[Authorize]
public sealed class TenantController(TenantService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/tenants/{tenantId}")]
    [RequireTenantAdmin]
    public async Task<IActionResult> Get(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        try
        {
            var tenant = await service.GetAsync(tenantId, ct);
            return tenant is null ? NotFound() : Ok(ToResponse(tenant));
        }
        catch (ArgumentException)
        {
            // Paths like "/tenants/me" reach this route; "me" fails the 3-char
            // minimum in CustomerStorageKey.Sanitise — treat as not found.
            return NotFound();
        }
    }

    // PLAT-seats (#710) — module selection for the tenant app. Any authenticated member of the
    // tenant may read which modules their tenant runs (the employee UI needs this to decide whether
    // to show a module switch), so this is intentionally broader than RequireTenantAdmin — but a
    // member can only read their OWN tenant's modules. No PII; just Parking/Seats.
    [HttpGet("/tenants/{tenantId}/modules")]
    public async Task<IActionResult> GetModules(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();
        if (!string.Equals(currentUser.TenantId, tenantId, StringComparison.Ordinal)) return Forbid();

        try
        {
            var tenant = await service.GetAsync(tenantId, ct);
            if (tenant is null) return NotFound();
            return Ok(new TenantModulesResponse(
                tenant.PrimaryModule.ToString(),
                tenant.EnabledModules.Select(m => m.ToString()).ToList(),
                tenant.DefaultLocale));
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpPut("/tenants/{tenantId}")]
    [RequireTenantAdmin]
    public async Task<IActionResult> Update(string tenantId, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var error = await service.UpdateAsync(
            tenantId, request.DisplayName, request.TimeZone,
            request.SupportContacts.Select(c => new TenantSupportContact(c.Name, c.Email, c.Role)).ToList(),
            ct, request.DefaultLocale);

        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpPost("/tenants/{tenantId}/transitions")]
    [RequireTenantAdmin]
    public async Task<IActionResult> Transition(string tenantId, [FromBody] TransitionRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        if (!Enum.TryParse<TenantLifecycleState>(request.To, ignoreCase: true, out var to))
            return BadRequest(new { error = $"Unknown lifecycle state: {request.To}" });

        var error = await service.TransitionAsync(tenantId, to, currentUser.UserId, request.Reason, request.Evidence, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpGet("/tenants/{tenantId}/transitions")]
    [RequireTenantAdmin]
    public async Task<IActionResult> GetTransitions(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var tenant = await service.GetAsync(tenantId, ct);
        if (tenant is null) return NotFound();
        return Ok(new { items = tenant.Transitions.Select(t => new
        {
            from = t.From.ToString(),
            to = t.To.ToString(),
            actorId = t.ActorId,
            occurredAt = t.OccurredAt,
            reason = t.Reason,
            evidence = t.Evidence,
        }) });
    }

    [HttpGet("/tenants/{tenantId}/provisioning")]
    [RequireTenantAdmin]
    public async Task<IActionResult> GetProvisioning(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var tenant = await service.GetAsync(tenantId, ct);
        if (tenant is null) return NotFound();
        var p = tenant.Provisioning;
        return Ok(new ProvisioningResponse(p.TenantId, p.TenantSlug, p.GeneratedAt, p.ServiceCollections));
    }

    [HttpPut("/tenants/{tenantId}/branding")]
    [RequireTenantAdmin]
    public async Task<IActionResult> SetBranding(string tenantId, [FromBody] SetBrandingRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        if (!Enum.TryParse<TenantLoginMode>(request.LoginMode, ignoreCase: true, out var loginMode))
            return BadRequest(new { error = $"Unknown login mode: {request.LoginMode}" });

        var config = new TenantBrandingConfig
        {
            PrimaryColor = request.PrimaryColor,
            AccentColor = request.AccentColor,
            LogoAssetId = request.LogoAssetId,
            FaviconAssetId = request.FaviconAssetId,
            LegalFooterText = request.LegalFooterText,
            LoginMode = loginMode,
        };

        var error = await service.SetBrandingAsync(tenantId, config, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpPost("/tenants/{tenantId}/discovery-domains")]
    [RequireTenantAdmin]
    public async Task<IActionResult> RegisterDiscoveryDomain(string tenantId, [FromBody] RegisterDiscoveryDomainRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var error = await service.RegisterDiscoveryDomainAsync(tenantId, request.Domain, Hash(currentUser.UserId), ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpDelete("/tenants/{tenantId}/discovery-domains/{domain}")]
    [RequireTenantAdmin]
    public async Task<IActionResult> UnregisterDiscoveryDomain(string tenantId, string domain, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var (found, error) = await service.UnregisterDiscoveryDomainAsync(tenantId, domain, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        if (!found) return NotFound();
        return NoContent();
    }

    private static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..16];

    private static TenantResponse ToResponse(TenantWorkspace t) => new(
        t.TenantId, t.Slug, t.DisplayName, t.Region, t.TimeZone,
        t.Kind.ToString(),
        t.LifecycleState.ToString(),
        t.SupportContacts.Select(c => new ContactDto(c.Name, c.Email, c.Role)).ToList(),
        t.Provisioning.ServiceCollections,
        t.PrimaryModule.ToString(),
        t.EnabledModules.Select(m => m.ToString()).ToList(),
        t.CreatedAt, t.UpdatedAt, t.DefaultLocale);
}

public sealed record UpdateTenantRequest(
    string DisplayName,
    string TimeZone,
    IReadOnlyList<ContactDto> SupportContacts,
    // LOC001 (#744): optional BCP 47 default locale, e.g. "cs-CZ". Null means "leave unchanged" —
    // unlike TimeZone (required, always overwritten), omitting this field on update never clears
    // an existing tenant default locale.
    string? DefaultLocale = null);

public sealed record TransitionRequest(string To, string? Reason, string? Evidence);

public sealed record ContactDto(string Name, string Email, string Role);

public sealed record TenantResponse(
    string TenantId,
    string Slug,
    string DisplayName,
    string Region,
    string TimeZone,
    string Kind,
    string LifecycleState,
    IReadOnlyList<ContactDto> SupportContacts,
    IReadOnlyDictionary<string, string> ServiceCollections,
    // PLAT007B — primary module (default landing / navigation emphasis) and all enabled modules
    // (primary first). Business-readable module names, e.g. "Parking", "Seats".
    string PrimaryModule,
    IReadOnlyList<string> EnabledModules,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    // LOC001 (#744): the tenant's BCP 47 default locale (e.g. "cs-CZ"); null when unset.
    string? DefaultLocale);

// PLAT-seats (#710) — the tenant's module selection for the tenant app UI.
// LOC001 (#744): also carries DefaultLocale so any tenant member can localize the app without a
// second call — this endpoint is intentionally readable by any authenticated member (see GetModules).
public sealed record TenantModulesResponse(string PrimaryModule, IReadOnlyList<string> EnabledModules, string? DefaultLocale);

public sealed record ProvisioningResponse(
    string TenantId,
    string TenantSlug,
    DateTimeOffset GeneratedAt,
    IReadOnlyDictionary<string, string> ServiceCollections);

public sealed record SetBrandingRequest(
    string? PrimaryColor,
    string? AccentColor,
    string? LogoAssetId,
    string? FaviconAssetId,
    string? LegalFooterText,
    string LoginMode = "LocalAccount");

public sealed record RegisterDiscoveryDomainRequest(string Domain);
