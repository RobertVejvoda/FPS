using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Identity;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

// PLAT001 / platformization: creating a tenant is a cross-tenant **platform-plane operator**
// operation (RequirePlatformAdmin), distinct from the tenant-scoped self-administration on
// TenantController. It is isolated here so it can move to the private fairspot-platform
// operator service (the open core bootstraps its single tenant via seed/config, not this API).
// Excluded from the open OpenAPI document (ApiExplorerSettings.IgnoreApi) so the generated
// open `@fps/api-client` does not expose a platform-plane endpoint (#673). The endpoint still
// serves at runtime; only its advertisement in the open client is withheld.
[ApiController]
[Authorize]
[Tags("Tenant")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class TenantProvisioningController(TenantService service, ICurrentUser currentUser) : ControllerBase
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
            ct, request.TenantId, kind, request.ResettableSandbox);

        if (error is not null) return BadRequest(new { error });
        // Location points at the tenant-scoped read on TenantController ("Tenant" controller, "Get" action).
        return CreatedAtAction("Get", "Tenant", new { tenantId = tenant!.TenantId }, ToResponse(tenant));
    }

    private static TenantResponse ToResponse(TenantWorkspace t) => new(
        t.TenantId, t.Slug, t.DisplayName, t.Region, t.TimeZone,
        t.Kind.ToString(),
        t.LifecycleState.ToString(),
        t.SupportContacts.Select(c => new ContactDto(c.Name, c.Email, c.Role)).ToList(),
        t.Provisioning.ServiceCollections,
        t.CreatedAt, t.UpdatedAt);
}

// Operator-only request body for tenant provisioning (moves to fairspot-platform with the controller).
public sealed record CreateTenantRequest(
    string? Slug,
    string DisplayName,
    string Region,
    string TimeZone,
    IReadOnlyList<ContactDto> SupportContacts,
    // Optional deterministic tenant ID for provisioning tools. If omitted, a GUID is generated.
    string? TenantId = null,
    // Tenant kind — defaults to Production for safety. Use Sandbox or Evaluation for demo tenants.
    string? Kind = null,
    // PLAT003A: mark this tenant as the resettable evaluation sandbox (Green Logistics). Only
    // honored when Kind==Sandbox; defaults false so real customer tenants are never resettable.
    bool ResettableSandbox = false);
