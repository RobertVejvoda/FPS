namespace FPS.Identity.Identity;

public interface IDeactivatedUserStore
{
    bool IsDeactivated(string tenantId, string userId);
    void Deactivate(string tenantId, string userId);
    void Reactivate(string tenantId, string userId);
}
