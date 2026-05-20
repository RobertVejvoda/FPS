namespace FPS.Identity.Identity;

public interface ITenantRoleMapper
{
    IReadOnlyList<string> MapToRoles(string tenantId, IEnumerable<string> incomingRoles);
}
