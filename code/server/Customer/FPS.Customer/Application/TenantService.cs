using FPS.Customer.Domain;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Customer.Application;

public sealed record TenantDiscoveryResponse(
    string Slug,
    string DisplayName,
    string LoginMode,
    string? PrimaryColor,
    string? AccentColor,
    string? LogoAssetId,
    string? FaviconAssetId,
    string? LegalFooterText);

public sealed class TenantService(
    ITenantRepository repository,
    TenantReadinessService? readinessService = null)
{
    public async Task<(TenantWorkspace? tenant, string? error)> CreateAsync(
        string? slug, string displayName, string region, string timeZone,
        IReadOnlyList<TenantSupportContact> supportContacts,
        CancellationToken ct,
        string? requestedTenantId = null,
        TenantKind kind = TenantKind.Production)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return (null, "Display name is required.");
        if (string.IsNullOrWhiteSpace(region)) return (null, "Region is required.");
        if (string.IsNullOrWhiteSpace(timeZone)) return (null, "Time zone is required.");

        var safeSlug = TenantProvisioningMetadata.Sanitize(slug);
        if (string.IsNullOrEmpty(safeSlug)) return (null, "Slug is required and must contain at least one alphanumeric character.");
        if (await repository.SlugExistsAsync(safeSlug, ct)) return (null, $"Slug '{safeSlug}' is already in use.");

        // Allow a deterministic tenant ID for provisioning tools; fall back to a generated GUID.
        string? safeTenantId = null;
        if (!string.IsNullOrWhiteSpace(requestedTenantId))
        {
            safeTenantId = TenantProvisioningMetadata.Sanitize(requestedTenantId);
            if (string.IsNullOrEmpty(safeTenantId))
                return (null, $"Requested tenant ID '{requestedTenantId}' is invalid after sanitisation (must contain at least one alphanumeric character).");
            if (await repository.GetAsync(safeTenantId, ct) is not null)
                return (null, $"Tenant ID '{safeTenantId}' is already in use.");
        }
        var tenantId = safeTenantId ?? Guid.NewGuid().ToString();
        // PLAT002: the tenant id is the canonical storage key (service Dapr keys + provisioning
        // scopes + purge all derive from it), so it must satisfy the tenant-storage contract
        // (3–63 chars, no reserved prefix). A generated GUID always passes; reject an invalid
        // requested id gracefully.
        try { _ = TenantStorageKey.Sanitise(tenantId); }
        catch (ArgumentException ex) { return (null, $"Tenant ID '{tenantId}' is not a valid tenant storage key: {ex.Message}"); }
        var tenant = new TenantWorkspace
        {
            TenantId = tenantId,
            Slug = safeSlug,
            DisplayName = displayName.Trim(),
            Region = region.Trim(),
            TimeZone = timeZone.Trim(),
            SupportContacts = supportContacts,
            Kind = kind,
            Provisioning = TenantProvisioningMetadata.Generate(tenantId, safeSlug),
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

        // Enforce full readiness before allowing Ready state.
        if (to == TenantLifecycleState.Ready && readinessService is not null)
        {
            var (report, readinessError) = await readinessService.CheckAsync(tenantId, dryRun: false, ct);
            if (readinessError is not null) return readinessError;
            if (!report!.IsReady)
            {
                var failed = report.Checks
                    .Where(c => c.Status == ReadinessStatus.Failed)
                    .Select(c => c.Name);
                return $"Tenant cannot become Ready. Failing checks: {string.Join(", ", failed)}.";
            }
        }

        var error = tenant.TryTransition(to, actorId, reason, evidence);
        if (error is not null) return error;

        await repository.SaveAsync(tenant, ct);
        return null;
    }

    public async Task<string?> SetBrandingAsync(string tenantId, TenantBrandingConfig config, CancellationToken ct)
    {
        var tenant = await repository.GetAsync(tenantId, ct);
        if (tenant is null) return "Tenant not found.";
        if (tenant.LifecycleState == TenantLifecycleState.Archived) return "Archived tenants cannot be updated.";

        var error = tenant.SetBranding(config);
        if (error is not null) return error;

        await repository.SaveAsync(tenant, ct);
        return null;
    }

    public async Task<string?> RegisterDiscoveryDomainAsync(
        string tenantId, string domain, string actorHash, CancellationToken ct)
    {
        var tenant = await repository.GetAsync(tenantId, ct);
        if (tenant is null) return "Tenant not found.";
        if (tenant.LifecycleState == TenantLifecycleState.Archived) return "Archived tenants cannot be updated.";

        if (await repository.IsDomainRegisteredAsync(domain, excludeTenantId: tenantId, ct))
            return $"Domain '{domain.Trim().ToLowerInvariant()}' is already registered by another tenant.";

        var error = tenant.AddDiscoveryDomain(domain, actorHash);
        if (error is not null) return error;

        await repository.SaveAsync(tenant, ct);
        return null;
    }

    public async Task<(bool found, string? error)> UnregisterDiscoveryDomainAsync(
        string tenantId, string domain, CancellationToken ct)
    {
        var tenant = await repository.GetAsync(tenantId, ct);
        if (tenant is null) return (false, "Tenant not found.");
        if (tenant.LifecycleState == TenantLifecycleState.Archived) return (false, "Archived tenants cannot be updated.");

        var removed = tenant.RemoveDiscoveryDomain(domain);
        if (!removed) return (false, null);

        await repository.SaveAsync(tenant, ct);
        return (true, null);
    }

    public async Task<TenantDiscoveryResponse?> DiscoverAsync(string domain, CancellationToken ct)
    {
        var tenant = await repository.FindByDiscoveryDomainAsync(domain, ct);
        if (tenant is null) return null;

        var b = tenant.Branding;
        return new TenantDiscoveryResponse(
            tenant.Slug,
            tenant.DisplayName,
            b.LoginMode.ToString(),
            b.PrimaryColor,
            b.AccentColor,
            b.LogoAssetId,
            b.FaviconAssetId,
            b.LegalFooterText);
    }
}
