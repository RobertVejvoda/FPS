using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
public sealed class TenantController(TenantService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("/tenants")]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var (tenant, error) = await service.CreateAsync(
            request.Slug, request.DisplayName, request.Region, request.TimeZone,
            request.SupportContacts.Select(c => new TenantSupportContact(c.Name, c.Email, c.Role)).ToList(),
            ct, request.TenantId);

        if (error is not null) return BadRequest(new { error });
        return CreatedAtAction(nameof(Get), new { tenantId = tenant!.TenantId }, ToResponse(tenant));
    }

    [HttpGet("/tenants/{tenantId}")]
    public async Task<IActionResult> Get(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var tenant = await service.GetAsync(tenantId, ct);
        return tenant is null ? NotFound() : Ok(ToResponse(tenant));
    }

    [HttpPut("/tenants/{tenantId}")]
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
    public async Task<IActionResult> GetProvisioning(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var tenant = await service.GetAsync(tenantId, ct);
        if (tenant is null) return NotFound();
        var p = tenant.Provisioning;
        return Ok(new ProvisioningResponse(p.TenantId, p.TenantSlug, p.GeneratedAt, p.ServiceCollections));
    }

    private static TenantResponse ToResponse(TenantWorkspace t) => new(
        t.TenantId, t.Slug, t.DisplayName, t.Region, t.TimeZone,
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
    string? TenantId = null);

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
