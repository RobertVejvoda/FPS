using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
public sealed class TenantIdentityController(
    TenantIdentityService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPut("/tenants/{tenantId}/identity-config")]
    public async Task<IActionResult> Configure(string tenantId, [FromBody] ConfigureIdentityRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        var config = new TenantIdentityConfig
        {
            TenantId = tenantId,
            TrustedIssuer = request.TrustedIssuer ?? string.Empty,
            Audience = request.Audience ?? string.Empty,
            TenantClaimName = request.TenantClaimName ?? "tenant_id",
            SubjectClaimName = request.SubjectClaimName ?? "sub",
            RoleClaimNames = request.RoleClaimNames ?? [],
            RoleMapping = request.RoleMapping ?? new Dictionary<string, string>(),
            LocalAccountPolicyEnabled = request.LocalAccountPolicyEnabled,
            ConfiguredByHash = Hash(currentUser.UserId),
            ConfiguredAt = DateTimeOffset.UtcNow,
        };

        var error = await service.ConfigureAsync(config, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpGet("/tenants/{tenantId}/identity-config")]
    public async Task<IActionResult> GetConfig(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var config = await service.GetConfigAsync(tenantId, ct);
        if (config is null) return NotFound();
        return Ok(Extensions.ToResponse(config));
    }

    [HttpPost("/tenants/{tenantId}/admins")]
    public async Task<IActionResult> RegisterAdmin(string tenantId, [FromBody] RegisterAdminRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId)) return Unauthorized();

        if (!Enum.TryParse<TenantAdminType>(request.AdminType, ignoreCase: true, out var adminType))
            return BadRequest(new { error = $"Unknown admin type: {request.AdminType}. Use SsoMapped or Local." });

        var admin = new TenantAdminRecord(
            TenantId: tenantId,
            SubjectHash: Hash(request.SubjectOrMarker ?? string.Empty),
            AdminType: adminType,
            CreatedByHash: Hash(currentUser.UserId),
            CreatedAt: DateTimeOffset.UtcNow,
            AuditNote: request.AuditNote,
            IsActive: true);

        var error = await service.RegisterAdminAsync(admin, ct);
        if (error == "Tenant not found.") return NotFound();
        if (error is not null) return BadRequest(new { error });
        return NoContent();
    }

    [HttpGet("/tenants/{tenantId}/admins")]
    public async Task<IActionResult> ListAdmins(string tenantId, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) return Unauthorized();

        var admins = await service.ListAdminsAsync(tenantId, ct);
        return Ok(new { items = admins.Select(a => new
        {
            subjectHash = a.SubjectHash,
            adminType = a.AdminType.ToString(),
            createdByHash = a.CreatedByHash,
            createdAt = a.CreatedAt,
            auditNote = a.AuditNote,
            isActive = a.IsActive,
        }) });
    }

    private static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..16];
}

public sealed record ConfigureIdentityRequest(
    string? TrustedIssuer,
    string? Audience,
    string? TenantClaimName,
    string? SubjectClaimName,
    IReadOnlyList<string>? RoleClaimNames,
    IReadOnlyDictionary<string, string>? RoleMapping,
    bool LocalAccountPolicyEnabled);

public sealed record RegisterAdminRequest(
    // Raw SSO subject or a local-account marker — will be hashed before storage.
    string? SubjectOrMarker,
    string? AdminType,
    string? AuditNote);

public sealed record IdentityConfigResponse(
    string TenantId,
    string TrustedIssuer,
    string Audience,
    string TenantClaimName,
    string SubjectClaimName,
    IReadOnlyList<string> RoleClaimNames,
    IReadOnlyDictionary<string, string> RoleMapping,
    bool LocalAccountPolicyEnabled,
    string ConfiguredByHash,
    DateTimeOffset ConfiguredAt,
    DateTimeOffset? UpdatedAt);

file static class Extensions
{
    public static IdentityConfigResponse ToResponse(TenantIdentityConfig c) => new(
        c.TenantId, c.TrustedIssuer, c.Audience,
        c.TenantClaimName, c.SubjectClaimName,
        c.RoleClaimNames, c.RoleMapping,
        c.LocalAccountPolicyEnabled,
        c.ConfiguredByHash, c.ConfiguredAt, c.UpdatedAt);
}
