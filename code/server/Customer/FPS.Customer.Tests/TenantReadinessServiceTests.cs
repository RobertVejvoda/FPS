using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

// Passing stubs for tests that simulate a fully connected deployment.
file sealed class PassProfileProbe : IProfileReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string t, CancellationToken c) =>
        Task.FromResult(ReadinessCheckResult.Pass("ProfileFacts"));
}
file sealed class PassBookingProbe : IBookingReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string t, CancellationToken c) =>
        Task.FromResult(ReadinessCheckResult.Pass("BookingSmokeTest"));
}
file sealed class PassNotificationProbe : INotificationReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string t, CancellationToken c) =>
        Task.FromResult(ReadinessCheckResult.Pass("NotificationReachable"));
}
file sealed class PassAuditProbe : IAuditReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string t, CancellationToken c) =>
        Task.FromResult(ReadinessCheckResult.Pass("AuditEvidence"));
}
file sealed class PassReportingProbe : IReportingReadinessProbe
{
    public Task<ReadinessCheckResult> CheckAsync(string t, CancellationToken c) =>
        Task.FromResult(ReadinessCheckResult.Pass("ReportingEvidence"));
}

public sealed class TenantReadinessServiceTests
{
    private readonly InMemoryTenantRepository tenantRepo = new();
    private readonly InMemoryTenantIdentityRepository identityRepo = new();
    private readonly InMemoryTenantParkingBootstrapRepository parkingRepo = new();

    // Uses no-op (failing) probes — simulates a deployment without connected services.
    private readonly TenantReadinessService service;
    // Uses passing probes — simulates a fully connected deployment.
    private readonly TenantReadinessService connectedService;

    public TenantReadinessServiceTests()
    {
        service = new TenantReadinessService(
            tenantRepo, identityRepo, parkingRepo,
            new NoOpProfileReadinessProbe(),
            new NoOpBookingReadinessProbe(),
            new NoOpNotificationReadinessProbe(),
            new NoOpAuditReadinessProbe(),
            new NoOpReportingReadinessProbe());

        connectedService = new TenantReadinessService(
            tenantRepo, identityRepo, parkingRepo,
            new PassProfileProbe(),
            new PassBookingProbe(),
            new PassNotificationProbe(),
            new PassAuditProbe(),
            new PassReportingProbe());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<string> SeedTenantAsync(
        TenantLifecycleState targetState = TenantLifecycleState.Seeded)
    {
        var tenant = new TenantWorkspace
        {
            TenantId = Guid.NewGuid().ToString(),
            Slug = $"acme-{Guid.NewGuid():N}",
            DisplayName = "ACME Corp",
            Region = "eu-west",
            TimeZone = "Europe/London",
        };
        if (targetState != TenantLifecycleState.Draft)
            tenant.TryTransition(TenantLifecycleState.Configured, "actor", null, null);
        if (targetState == TenantLifecycleState.Seeded || targetState == TenantLifecycleState.Ready)
            tenant.TryTransition(TenantLifecycleState.Seeded, "actor", null, null);
        if (targetState == TenantLifecycleState.Ready)
            tenant.TryTransition(TenantLifecycleState.Ready, "actor", null, null);
        if (targetState == TenantLifecycleState.Suspended)
            tenant.TryTransition(TenantLifecycleState.Suspended, "actor", null, null);
        if (targetState == TenantLifecycleState.Archived)
        {
            tenant.TryTransition(TenantLifecycleState.Suspended, "actor", null, null);
            tenant.TryTransition(TenantLifecycleState.Archived, "actor", null, null);
        }
        await tenantRepo.SaveAsync(tenant, CancellationToken.None);
        return tenant.TenantId;
    }

    private async Task SeedIdentityAsync(string tenantId,
        string issuer = "https://idp.example.com",
        string audience = "fairspot-api",
        IReadOnlyDictionary<string, string>? roleMapping = null)
    {
        var config = new TenantIdentityConfig
        {
            TenantId = tenantId,
            TrustedIssuer = issuer,
            Audience = audience,
            SubjectClaimName = "sub",
            RoleMapping = roleMapping ?? new Dictionary<string, string>
                { { "grp-admin", "admin" }, { "grp-employee", "employee" } },
            ConfiguredByHash = "actor-hash",
            ConfiguredAt = DateTimeOffset.UtcNow,
        };
        await identityRepo.SaveConfigAsync(config, CancellationToken.None);
    }

    private async Task SeedAdminAsync(string tenantId)
    {
        var admin = new TenantAdminRecord(tenantId, "admin-hash-001",
            TenantAdminType.SsoMapped, "actor-hash", DateTimeOffset.UtcNow, null, IsActive: true);
        await identityRepo.SaveAdminAsync(admin, CancellationToken.None);
    }

    private async Task SeedParkingAsync(string tenantId)
    {
        var bootstrap = await parkingRepo.GetOrCreateAsync(tenantId, CancellationToken.None);
        bootstrap.RecordDefaultPolicy(new BootstrapPolicySnapshot(
            "Europe/London", "18:00", 10, 30, "actor-hash", DateTimeOffset.UtcNow));
        bootstrap.RecordLocation("loc-001", 20, false, "actor-hash");
        await parkingRepo.SaveAsync(bootstrap, CancellationToken.None);
    }

    private async Task<string> FullyConfiguredTenantAsync()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Seeded);
        await SeedIdentityAsync(tenantId);
        await SeedAdminAsync(tenantId);
        await SeedParkingAsync(tenantId);
        return tenantId;
    }

    // ── not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_UnknownTenant_ReturnsError()
    {
        var (report, error) = await service.CheckAsync("no-such-tenant", false, CancellationToken.None);
        Assert.Null(report);
        Assert.Contains("not found", error);
    }

    // ── lifecycle check ───────────────────────────────────────────────────────

    [Fact]
    public async Task Check_DraftTenant_LifecycleCheckFails()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Draft);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var lc = report!.Checks.Single(c => c.Name == "LifecycleState");
        Assert.Equal(ReadinessStatus.Failed, lc.Status);
        Assert.False(report.IsReady);
    }

    [Fact]
    public async Task Check_SuspendedTenant_LifecycleCheckFails()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Suspended);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var lc = report!.Checks.Single(c => c.Name == "LifecycleState");
        Assert.Equal(ReadinessStatus.Failed, lc.Status);
    }

    [Fact]
    public async Task Check_ConfiguredTenant_LifecycleCheckPasses()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var lc = report!.Checks.Single(c => c.Name == "LifecycleState");
        Assert.Equal(ReadinessStatus.Passed, lc.Status);
    }

    // ── identity check ────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_NoIdentityConfig_IdentityCheckFails()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var id = report!.Checks.Single(c => c.Name == "IdentityConfig");
        Assert.Equal(ReadinessStatus.Failed, id.Status);
    }

    [Fact]
    public async Task Check_IdentityConfigured_IdentityCheckPasses()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        await SeedIdentityAsync(tenantId);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var id = report!.Checks.Single(c => c.Name == "IdentityConfig");
        Assert.Equal(ReadinessStatus.Passed, id.Status);
    }

    // ── admin check ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_NoActiveAdmin_AdminCheckFails()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        await SeedIdentityAsync(tenantId);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var adm = report!.Checks.Single(c => c.Name == "ActiveAdmin");
        Assert.Equal(ReadinessStatus.Failed, adm.Status);
    }

    [Fact]
    public async Task Check_WithActiveAdmin_AdminCheckPasses()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        await SeedIdentityAsync(tenantId);
        await SeedAdminAsync(tenantId);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var adm = report!.Checks.Single(c => c.Name == "ActiveAdmin");
        Assert.Equal(ReadinessStatus.Passed, adm.Status);
    }

    // ── role mapping check ────────────────────────────────────────────────────

    [Fact]
    public async Task Check_RoleMappingWithUnknownRole_RoleMappingFails()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        await SeedIdentityAsync(tenantId, roleMapping: new Dictionary<string, string>
            { { "grp-x", "superuser" } });
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var rm = report!.Checks.Single(c => c.Name == "RoleMapping");
        Assert.Equal(ReadinessStatus.Failed, rm.Status);
        Assert.Contains("superuser", rm.Reason);
    }

    [Fact]
    public async Task Check_ValidRoleMapping_RoleMappingPasses()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        await SeedIdentityAsync(tenantId);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var rm = report!.Checks.Single(c => c.Name == "RoleMapping");
        Assert.Equal(ReadinessStatus.Passed, rm.Status);
    }

    [Fact]
    public async Task Check_AuditorRoleMapping_RoleMappingPasses()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        await SeedIdentityAsync(tenantId, roleMapping: new Dictionary<string, string>
        {
            { "grp-admin", "admin" },
            { "grp-employee", "employee" },
            { "grp-auditor", "auditor" },
            { "grp-report-viewer", "report_viewer" },
        });
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var rm = report!.Checks.Single(c => c.Name == "RoleMapping");
        Assert.Equal(ReadinessStatus.Passed, rm.Status);
    }

    [Fact]
    public async Task Check_NoIdentity_RoleMappingSkipped()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var rm = report!.Checks.Single(c => c.Name == "RoleMapping");
        Assert.Equal(ReadinessStatus.Skipped, rm.Status);
    }

    // ── parking checks ────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_NoParkingPolicy_ParkingPolicyFails()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var pp = report!.Checks.Single(c => c.Name == "ParkingPolicy");
        Assert.Equal(ReadinessStatus.Failed, pp.Status);
    }

    [Fact]
    public async Task Check_ParkingPolicyNoLocation_LocationFails()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        var bootstrap = await parkingRepo.GetOrCreateAsync(tenantId, CancellationToken.None);
        bootstrap.RecordDefaultPolicy(new BootstrapPolicySnapshot(
            "Europe/London", "18:00", 10, 30, "actor", DateTimeOffset.UtcNow));
        await parkingRepo.SaveAsync(bootstrap, CancellationToken.None);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var pp = report!.Checks.Single(c => c.Name == "ParkingPolicy");
        var pl = report.Checks.Single(c => c.Name == "ParkingLocation");
        Assert.Equal(ReadinessStatus.Passed, pp.Status);
        Assert.Equal(ReadinessStatus.Failed, pl.Status);
    }

    // ── out-of-process probes block Ready when not connected ──────────────────

    [Fact]
    public async Task Check_OutOfProcessProbes_FailWhenNotConnected()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var probeNames = new[] { "ProfileFacts", "BookingSmokeTest", "NotificationReachable",
            "AuditEvidence", "ReportingEvidence" };
        foreach (var name in probeNames)
        {
            var check = report!.Checks.Single(c => c.Name == name);
            Assert.Equal(ReadinessStatus.Failed, check.Status);
            Assert.Contains("not connected", check.Reason);
        }
    }

    [Fact]
    public async Task Check_OutOfProcessProbesNotConnected_BlocksIsReady()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        // No-op probes return Failed — local checks pass but overall not ready.
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        Assert.False(report!.IsReady);
        Assert.All(report.Checks.Where(c => c.Name is "LifecycleState" or "IdentityConfig"
                or "ActiveAdmin" or "RoleMapping" or "ParkingPolicy" or "ParkingLocation"),
            c => Assert.Equal(ReadinessStatus.Passed, c.Status));
        // Pilot-deferred checks are always Deferred, regardless of probe connectivity.
        Assert.All(report.Checks.Where(c => c.Name is "ObjectStorageReadiness" or "BrandingReadiness"),
            c => Assert.Equal(ReadinessStatus.Deferred, c.Status));
    }

    // ── pilot-deferred checks ─────────────────────────────────────────────────

    [Fact]
    public async Task Check_ObjectStorageReadiness_IsAlwaysDeferred()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        var (report, _) = await connectedService.CheckAsync(tenantId, false, CancellationToken.None);
        var check = report!.Checks.Single(c => c.Name == "ObjectStorageReadiness");
        Assert.Equal(ReadinessStatus.Deferred, check.Status);
        Assert.Contains("Pilot limitation", check.Reason);
        Assert.Contains("OPS008C", check.Reason);
    }

    [Fact]
    public async Task Check_BrandingReadiness_IsAlwaysDeferred()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        var (report, _) = await connectedService.CheckAsync(tenantId, false, CancellationToken.None);
        var check = report!.Checks.Single(c => c.Name == "BrandingReadiness");
        Assert.Equal(ReadinessStatus.Deferred, check.Status);
        Assert.Contains("Pilot limitation", check.Reason);
        Assert.Contains("CUST010", check.Reason);
    }

    [Fact]
    public async Task Check_DeferredChecks_DoNotBlockIsReady()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        var (report, _) = await connectedService.CheckAsync(tenantId, false, CancellationToken.None);
        Assert.True(report!.IsReady);
        var deferred = report.Checks.Where(c => c.Status == ReadinessStatus.Deferred).ToList();
        Assert.NotEmpty(deferred);
    }

    [Fact]
    public async Task Check_Output_DoesNotLeakStoragePaths()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        var (report, _) = await connectedService.CheckAsync(tenantId, false, CancellationToken.None);
        foreach (var check in report!.Checks)
        {
            Assert.DoesNotContain("minio", check.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("s3://", check.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bucket", check.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── full pass (connected deployment) ─────────────────────────────────────

    [Fact]
    public async Task Check_AllChecksPassing_IsReadyTrue()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        var (report, _) = await connectedService.CheckAsync(tenantId, false, CancellationToken.None);
        Assert.True(report!.IsReady);
        // Deferred pilot-limitation checks (ObjectStorageReadiness, BrandingReadiness) are
        // non-blocking: they appear in the report but do not prevent IsReady.
        Assert.All(report.Checks, c => Assert.True(
            c.Status is ReadinessStatus.Passed or ReadinessStatus.Deferred,
            $"Expected Passed or Deferred for '{c.Name}' but got {c.Status}"));
    }

    // ── dry-run ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_DryRun_ReportsIsDryRunTrue()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        var (report, _) = await connectedService.CheckAsync(tenantId, dryRun: true, CancellationToken.None);
        Assert.True(report!.IsDryRun);
        Assert.True(report.IsReady);
    }

    [Fact]
    public async Task Check_DryRunFailing_ReportsIsDryRunTrueAndNotReady()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Draft);
        var (report, _) = await service.CheckAsync(tenantId, dryRun: true, CancellationToken.None);
        Assert.True(report!.IsDryRun);
        Assert.False(report.IsReady);
    }

    [Fact]
    public async Task Check_DryRun_DoesNotCreateBootstrapRecord()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        // Tenant has no parking bootstrap — readiness check must not create one.
        await service.CheckAsync(tenantId, dryRun: true, CancellationToken.None);
        var existing = await parkingRepo.GetAsync(tenantId, CancellationToken.None);
        Assert.Null(existing);
    }

    [Fact]
    public async Task Check_NonDryRun_DoesNotCreateBootstrapRecord()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        await service.CheckAsync(tenantId, dryRun: false, CancellationToken.None);
        var existing = await parkingRepo.GetAsync(tenantId, CancellationToken.None);
        Assert.Null(existing);
    }

    // ── transition guard ──────────────────────────────────────────────────────

    [Fact]
    public async Task TransitionToReady_WhenReadinessFails_ReturnsError()
    {
        // TenantService wired with a connected readiness service but no identity/admin/parking seeded.
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Seeded);
        var tenantService = new TenantService(tenantRepo, connectedService);
        var error = await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Ready,
            "actor", null, null, CancellationToken.None);
        Assert.NotNull(error);
        Assert.Contains("cannot become Ready", error);
        Assert.Contains("Failing checks", error);
    }

    [Fact]
    public async Task TransitionToReady_WhenAllChecksPassing_Succeeds()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        var tenantService = new TenantService(tenantRepo, connectedService);
        var error = await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Ready,
            "actor", null, null, CancellationToken.None);
        Assert.Null(error);
        var tenant = await tenantRepo.GetAsync(tenantId, CancellationToken.None);
        Assert.Equal(TenantLifecycleState.Ready, tenant!.LifecycleState);
    }

    // ── tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Check_TenantIsolation_OtherTenantDataNotCrossed()
    {
        var tenantA = await FullyConfiguredTenantAsync();
        var tenantB = await SeedTenantAsync(TenantLifecycleState.Configured);

        var (reportA, _) = await connectedService.CheckAsync(tenantA, false, CancellationToken.None);
        var (reportB, _) = await connectedService.CheckAsync(tenantB, false, CancellationToken.None);

        Assert.True(reportA!.IsReady);
        Assert.False(reportB!.IsReady);
    }

    // ── secrets / masking ─────────────────────────────────────────────────────

    [Fact]
    public async Task Check_Output_DoesNotLeakIssuerOrAudience()
    {
        var tenantId = await FullyConfiguredTenantAsync();
        var (report, _) = await connectedService.CheckAsync(tenantId, false, CancellationToken.None);
        foreach (var check in report!.Checks)
        {
            Assert.DoesNotContain("https://idp.example.com", check.Reason ?? string.Empty);
            Assert.DoesNotContain("fairspot-api", check.Reason ?? string.Empty);
        }
    }
}
