using FPS.SharedKernel.Identity;
using System.Security.Claims;

namespace FPS.Profile.Identity;

public sealed class CurrentUser : ICurrentUser
{
    public string UserId { get; }
    public string TenantId { get; }
    public IReadOnlyList<string> Roles { get; }
    public bool IsAuthenticated { get; }
    public string? DisplayName { get; }

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        IsAuthenticated = principal?.Identity?.IsAuthenticated ?? false;
        UserId = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub") ?? string.Empty;
        TenantId = principal?.FindFirstValue("tenant_id") ?? string.Empty;
        Roles = principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
        DisplayName = ResolveDisplayName(principal);
    }

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private static string? ResolveDisplayName(ClaimsPrincipal? principal)
    {
        if (principal is null) return null;

        var name = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();

        var given = principal.FindFirstValue("given_name") ?? principal.FindFirstValue(ClaimTypes.GivenName);
        var family = principal.FindFirstValue("family_name") ?? principal.FindFirstValue(ClaimTypes.Surname);
        var combined = $"{given} {family}".Trim();
        return string.IsNullOrWhiteSpace(combined) ? null : combined;
    }
}
