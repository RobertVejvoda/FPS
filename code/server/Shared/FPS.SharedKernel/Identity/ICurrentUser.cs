namespace FPS.SharedKernel.Identity;

public interface ICurrentUser
{
    string UserId { get; }
    string TenantId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    // Display name derived from authenticated JWT name claims. Null when claims are absent.
    // Only Profile service reads this; other services return null.
    string? DisplayName { get; }
    bool IsInRole(string role);
}
