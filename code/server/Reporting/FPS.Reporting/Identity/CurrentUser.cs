using FPS.SharedKernel.Identity;
using System.Security.Claims;

namespace FPS.Reporting.Identity;

public sealed class CurrentUser : ICurrentUser
{
    public string UserId { get; }
    public string TenantId { get; }
    public IReadOnlyList<string> Roles { get; }
    public bool IsAuthenticated { get; }

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        IsAuthenticated = principal?.Identity?.IsAuthenticated ?? false;

        UserId = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue("sub")
            ?? string.Empty;

        TenantId = principal?.FindFirstValue("tenant_id") ?? string.Empty;

        Roles = principal?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? [];
    }

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
