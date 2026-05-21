using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public sealed class TenantService(ITenantRepository repository)
{
    public async Task<(TenantWorkspace? tenant, string? error)> CreateAsync(
        string slug, string displayName, string region, string timeZone,
        IReadOnlyList<TenantSupportContact> supportContacts,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) return (null, "Slug is required.");
        if (string.IsNullOrWhiteSpace(displayName)) return (null, "Display name is required.");
        if (string.IsNullOrWhiteSpace(region)) return (null, "Region is required.");
        if (string.IsNullOrWhiteSpace(timeZone)) return (null, "Time zone is required.");
        if (await repository.SlugExistsAsync(slug, ct)) return (null, $"Slug '{slug}' is already in use.");

        var tenant = new TenantWorkspace
        {
            TenantId = Guid.NewGuid().ToString(),
            Slug = slug.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            Region = region.Trim(),
            TimeZone = timeZone.Trim(),
            SupportContacts = supportContacts,
        };

        await repository.SaveAsync(tenant, ct);
        return (tenant, null);
    }

    public async Task<TenantWorkspace?> GetAsync(string tenantId, CancellationToken ct) =>
        await repository.GetAsync(tenantId, ct);

    public async Task<string?> UpdateAsync(
        string tenantId, string displayName, string timeZone,
        IReadOnlyList<TenantSupportContact> supportContacts,
        CancellationToken ct)
    {
        var tenant = await repository.GetAsync(tenantId, ct);
        if (tenant is null) return "Tenant not found.";
        if (string.IsNullOrWhiteSpace(displayName)) return "Display name is required.";
        if (string.IsNullOrWhiteSpace(timeZone)) return "Time zone is required.";
        if (tenant.LifecycleState == TenantLifecycleState.Archived) return "Archived tenants cannot be updated.";

        tenant.DisplayName = displayName.Trim();
        tenant.TimeZone = timeZone.Trim();
        tenant.SupportContacts = supportContacts;
        tenant.Touch();
        await repository.SaveAsync(tenant, ct);
        return null;
    }

    public async Task<string?> TransitionAsync(
        string tenantId, TenantLifecycleState to, string actorId, string? reason, string? evidence,
        CancellationToken ct)
    {
        var tenant = await repository.GetAsync(tenantId, ct);
        if (tenant is null) return "Tenant not found.";

        var error = tenant.TryTransition(to, actorId, reason, evidence);
        if (error is not null) return error;

        await repository.SaveAsync(tenant, ct);
        return null;
    }
}
