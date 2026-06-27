using FPS.Customer.Domain;
using FPS.SharedKernel.Identity;

namespace FPS.Customer.Application;

public sealed class TenantIdentityService(
    ITenantIdentityRepository repository,
    ITenantRepository tenantRepository,
    InMemoryTenantIdentityConfigStore configStore,
    InMemoryTenantRoleMappingStore roleMappingStore)
{
    public async Task<string?> ConfigureAsync(TenantIdentityConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.TrustedIssuer))
            return "Trusted issuer is required.";
        if (string.IsNullOrWhiteSpace(config.Audience))
            return "Audience is required.";
        if (string.IsNullOrWhiteSpace(config.SubjectClaimName))
            return "Subject claim name is required.";

        var tenant = await tenantRepository.GetAsync(config.TenantId, ct);
        if (tenant is null) return "Tenant not found.";
        if (tenant.LifecycleState == TenantLifecycleState.Archived)
            return "Cannot configure identity for an archived tenant.";

        // Write-through: Dapr repository is written first; in-memory stores are
        // updated only after the durable write succeeds. The in-memory stores are
        // never mutated directly — this is the only mutation path (PERSIST006B).
        await repository.SaveConfigAsync(config, ct);
        configStore.Register(config.TenantId);
        roleMappingStore.SetMapping(config.TenantId, config.RoleMapping);
        configStore.SetClaimConfig(config.TenantId, new TenantClaimConfig(
            config.TenantClaimName, config.SubjectClaimName, config.RoleClaimNames));
        return null;
    }

    public async Task<TenantIdentityConfig?> GetConfigAsync(string tenantId, CancellationToken ct) =>
        await repository.GetConfigAsync(tenantId, ct);

    public async Task<string?> RegisterAdminAsync(TenantAdminRecord admin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(admin.SubjectHash))
            return "Subject hash is required.";

        var config = await repository.GetConfigAsync(admin.TenantId, ct);

        if (admin.AdminType == TenantAdminType.Local)
        {
            if (config is null || !config.LocalAccountPolicyEnabled)
                return "Local accounts are not permitted for this tenant. Enable local account policy first.";
        }
        else
        {
            if (config is null)
                return "Identity must be configured before registering an SSO admin.";
        }

        var existing = await repository.GetAdminsAsync(admin.TenantId, ct);
        if (existing.Any(a => a.SubjectHash == admin.SubjectHash && a.IsActive))
            return "This subject is already registered as an active admin.";

        await repository.SaveAdminAsync(admin, ct);
        return null;
    }

    public async Task<IReadOnlyList<TenantAdminRecord>> ListAdminsAsync(string tenantId, CancellationToken ct) =>
        await repository.GetAdminsAsync(tenantId, ct);

    public async Task<bool> HasActiveAdminAsync(string tenantId, CancellationToken ct)
    {
        var admins = await repository.GetAdminsAsync(tenantId, ct);
        return admins.Any(a => a.IsActive);
    }
}
