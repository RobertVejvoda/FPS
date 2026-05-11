namespace FPS.Identity.Models;

public sealed record MeResponse(string UserId, string TenantId, IReadOnlyList<string> Roles);
