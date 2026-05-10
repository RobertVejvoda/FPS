namespace FPS.SharedKernel.Identity;

public interface ICurrentUser
{
    string UserId { get; }
    string TenantId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
