using FPS.Customer.Domain;

namespace FPS.Customer.Application;

public enum ReadinessStatus { Passed, Failed, Skipped, Deferred }

public sealed record ReadinessCheckResult(string Name, ReadinessStatus Status, string Reason)
{
    public static ReadinessCheckResult Pass(string name) =>
        new(name, ReadinessStatus.Passed, string.Empty);

    public static ReadinessCheckResult Fail(string name, string reason) =>
        new(name, ReadinessStatus.Failed, reason);

    public static ReadinessCheckResult Skipped(string name, string reason) =>
        new(name, ReadinessStatus.Skipped, reason);

    public static ReadinessCheckResult Defer(string name, string reason) =>
        new(name, ReadinessStatus.Deferred, reason);
}

public sealed record ReadinessReport(
    string TenantId,
    bool IsDryRun,
    bool IsReady,
    IReadOnlyList<ReadinessCheckResult> Checks);

file static class KnownFpsRoles
{
    internal static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        { "employee", "hr_manager", "admin", "report_viewer" };
}

public sealed class TenantReadinessService(
    ITenantRepository tenantRepository,
    ITenantIdentityRepository identityRepository,
    ITenantParkingBootstrapRepository parkingBootstrapRepository,
    IProfileReadinessProbe profileProbe,
    IBookingReadinessProbe bookingProbe,
    INotificationReadinessProbe notificationProbe,
    IAuditReadinessProbe auditProbe,
    IReportingReadinessProbe reportingProbe)
{
    public async Task<(ReadinessReport? report, string? error)> CheckAsync(
        string tenantId, bool dryRun, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(tenantId, ct);
        if (tenant is null) return (null, "Tenant not found.");

        var checks = new List<ReadinessCheckResult>
        {
            CheckLifecycle(tenant),
            await CheckIdentityAsync(tenantId, ct),
            await CheckAdminAsync(tenantId, ct),
            await CheckRoleMappingAsync(tenantId, ct),
            await CheckParkingPolicyAsync(tenantId, ct),
            await CheckParkingLocationAsync(tenantId, ct),
            CheckObjectStorageReadiness(),
            CheckBrandingReadiness(),
            await profileProbe.CheckAsync(tenantId, ct),
            await bookingProbe.CheckAsync(tenantId, ct),
            await notificationProbe.CheckAsync(tenantId, ct),
            await auditProbe.CheckAsync(tenantId, ct),
            await reportingProbe.CheckAsync(tenantId, ct),
        };

        var isReady = checks.All(c => c.Status is not ReadinessStatus.Failed);
        return (new ReadinessReport(tenantId, dryRun, isReady, checks), null);
    }

    private static ReadinessCheckResult CheckLifecycle(TenantWorkspace tenant)
    {
        if (tenant.LifecycleState is TenantLifecycleState.Draft)
            return ReadinessCheckResult.Fail("LifecycleState",
                "Tenant is in Draft state. Complete configuration before running a readiness check.");
        if (tenant.LifecycleState is TenantLifecycleState.Suspended)
            return ReadinessCheckResult.Fail("LifecycleState",
                "Tenant is Suspended. Restore it before it can become Ready.");
        if (tenant.LifecycleState is TenantLifecycleState.Archived)
            return ReadinessCheckResult.Fail("LifecycleState",
                "Tenant is Archived and cannot become Ready.");
        return ReadinessCheckResult.Pass("LifecycleState");
    }

    private async Task<ReadinessCheckResult> CheckIdentityAsync(string tenantId, CancellationToken ct)
    {
        var config = await identityRepository.GetConfigAsync(tenantId, ct);
        if (config is null)
            return ReadinessCheckResult.Fail("IdentityConfig",
                "No identity configuration found. Configure trusted issuer and audience.");
        if (string.IsNullOrWhiteSpace(config.TrustedIssuer))
            return ReadinessCheckResult.Fail("IdentityConfig", "Trusted issuer is not set.");
        if (string.IsNullOrWhiteSpace(config.Audience))
            return ReadinessCheckResult.Fail("IdentityConfig", "Audience is not set.");
        return ReadinessCheckResult.Pass("IdentityConfig");
    }

    private async Task<ReadinessCheckResult> CheckAdminAsync(string tenantId, CancellationToken ct)
    {
        var admins = await identityRepository.GetAdminsAsync(tenantId, ct);
        if (!admins.Any(a => a.IsActive))
            return ReadinessCheckResult.Fail("ActiveAdmin",
                "No active administrator found. Register at least one tenant administrator.");
        return ReadinessCheckResult.Pass("ActiveAdmin");
    }

    private async Task<ReadinessCheckResult> CheckRoleMappingAsync(string tenantId, CancellationToken ct)
    {
        var config = await identityRepository.GetConfigAsync(tenantId, ct);
        if (config is null)
            return ReadinessCheckResult.Skipped("RoleMapping",
                "Skipped: identity is not configured.");

        var unknownRoles = config.RoleMapping.Values
            .Where(r => !KnownFpsRoles.All.Contains(r))
            .ToList();

        if (unknownRoles.Count > 0)
            return ReadinessCheckResult.Fail("RoleMapping",
                $"Role mapping references unknown FPS roles: {string.Join(", ", unknownRoles)}.");

        return ReadinessCheckResult.Pass("RoleMapping");
    }

    private async Task<ReadinessCheckResult> CheckParkingPolicyAsync(string tenantId, CancellationToken ct)
    {
        var bootstrap = await parkingBootstrapRepository.GetAsync(tenantId, ct);
        if (bootstrap is null || !bootstrap.DefaultPolicyConfigured)
            return ReadinessCheckResult.Fail("ParkingPolicy",
                "Parking default policy has not been bootstrapped.");
        return ReadinessCheckResult.Pass("ParkingPolicy");
    }

    private async Task<ReadinessCheckResult> CheckParkingLocationAsync(string tenantId, CancellationToken ct)
    {
        var bootstrap = await parkingBootstrapRepository.GetAsync(tenantId, ct);
        if (bootstrap is null || !bootstrap.HasUsableLocation)
            return ReadinessCheckResult.Fail("ParkingLocation",
                "No location with active slots found. Add at least one location with capacity.");
        return ReadinessCheckResult.Pass("ParkingLocation");
    }

    private static ReadinessCheckResult CheckObjectStorageReadiness() =>
        ReadinessCheckResult.Defer("ObjectStorageReadiness",
            "Pilot limitation: Object storage provisioning is not yet implemented. " +
            "Tenant document uploads, report exports, audit evidence exports, and branding uploads " +
            "are unavailable during the pilot. Resolve before production (OPS008C).");

    private static ReadinessCheckResult CheckBrandingReadiness() =>
        ReadinessCheckResult.Defer("BrandingReadiness",
            "Pilot limitation: Organization branding is not configured. " +
            "FairSpot defaults (name, generic styling) will be used during the pilot. " +
            "Resolve before production (CUST010).");
}
