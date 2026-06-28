using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

// PLAT001: creating a tenant is a platform-plane operation (RequirePlatformAdmin);
// every /tenants/{tenantId} operation is tenant-scoped (RequireTenantAdmin =
// platform_admin cross-tenant, or the tenant's own admin).
[ApiController]
[Authorize]
public sealed class TenantController(TenantService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("/tenants")]
    [RequirePlatformAdmin]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        if (!Enum.TryParse<TenantKind>(request.Kind ?? "Production", ignoreCase: true, out var kind))
            return BadRequest(new { error = $"Unknown tenant kind: {request.Kind}" });

        var (tenant, error) = await service.CreateAsync(
            request.Slug, request.DisplayName, request.Region, request.TimeZone,
            request.SupportContacts.Select(c => new TenantSupportContact(c.Name, c.Email, c.Role)).ToList(),
            ct, request.TenantId, kind);

        if (error is not null) return BadRequest(new { error });
        return CreatedAtAction(nameof(Get), new { tenantId = tenant!.TenantId }, ToResponse(tenant));
    }

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

    [HttpPut("/tenants/{tenantId}")]
    [RequireTenantAdmin]
    public async Task<IActionResult> Update(string tenantId, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var error = await service.UpdateAsync(
            tenantId, request.DisplayName, request.TimeZone,
            request.SupportContacts.Select(c => new TenantSupportContact(c.Name, c.Email, c.Role)).ToList(),
            ct);

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
        t.CreatedAt, t.UpdatedAt);
}

public sealed record CreateTenantRequest(
    string? Slug,
    string DisplayName,
    string Region,
    string TimeZone,
    IReadOnlyList<ContactDto> SupportContacts,
    // Optional deterministic tenant ID for provisioning tools. If omitted, a GUID is generated.
    string? TenantId = null,
    // Tenant kind — defaults to Production for safety. Use Sandbox or Evaluation for demo tenants.
    string? Kind = null);

public sealed record UpdateTenantRequest(
    string DisplayName,
    string TimeZone,
    IReadOnlyList<ContactDto> SupportContacts);

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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

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
