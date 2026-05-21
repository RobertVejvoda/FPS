namespace FPS.SharedKernel.Identity;

public interface ITenantIdentityConfigStore
{
    // True once at least one tenant has been configured; enables enforcement.
    bool IsEnforcementActive { get; }
    bool IsConfigured(string tenantId);
}
