using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

public sealed class TenantReadinessServiceTests
{
    private readonly InMemoryTenantRepository tenantRepo = new();
    private readonly InMemoryTenantIdentityRepository identityRepo = new();
    private readonly InMemoryTenantParkingBootstrapRepository parkingRepo = new();
    private readonly TenantReadinessService service;

    public TenantReadinessServiceTests()
    {
        service = new TenantReadinessService(
            tenantRepo, identityRepo, parkingRepo,
            new NoOpProfileReadinessProbe(),
            new NoOpBookingReadinessProbe(),
            new NoOpNotificationReadinessProbe(),
            new NoOpAuditReadinessProbe(),
            new NoOpReportingReadinessProbe());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<string> SeedTenantAsync(
        TenantLifecycleState targetState = TenantLifecycleState.Seeded)
    {
        var tenant = new TenantWorkspace
        {
            TenantId = Guid.NewGuid().ToString(),
            Slug = "acme",
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
        string audience = "fps-api",
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

    private async Task<string> FullyReadyTenantAsync()
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

    // ── out-of-process probes skipped ─────────────────────────────────────────

    [Fact]
    public async Task Check_OutOfProcessProbes_AreSkipped()
    {
        var tenantId = await SeedTenantAsync(TenantLifecycleState.Configured);
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        var skippedNames = new[] { "ProfileFacts", "BookingSmokeTest", "NotificationReachable",
            "AuditEvidence", "ReportingEvidence" };
        foreach (var name in skippedNames)
        {
            var check = report!.Checks.Single(c => c.Name == name);
            Assert.Equal(ReadinessStatus.Skipped, check.Status);
        }
    }

    // ── full pass ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_AllLocalChecksPassing_IsReadyTrue()
    {
        var tenantId = await FullyReadyTenantAsync();
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        Assert.True(report!.IsReady);
        Assert.All(report.Checks, c =>
            Assert.NotEqual(ReadinessStatus.Failed, c.Status));
    }

    // ── dry-run ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_DryRun_ReportsIsDryRunTrue()
    {
        var tenantId = await FullyReadyTenantAsync();
        var (report, _) = await service.CheckAsync(tenantId, dryRun: true, CancellationToken.None);
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

    // ── tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Check_TenantIsolation_OtherTenantDataNotCrossed()
    {
        var tenantA = await FullyReadyTenantAsync();
        var tenantB = await SeedTenantAsync(TenantLifecycleState.Configured);

        var (reportA, _) = await service.CheckAsync(tenantA, false, CancellationToken.None);
        var (reportB, _) = await service.CheckAsync(tenantB, false, CancellationToken.None);

        Assert.True(reportA!.IsReady);
        Assert.False(reportB!.IsReady);
    }

    // ── secrets / masking ─────────────────────────────────────────────────────

    [Fact]
    public async Task Check_Output_DoesNotLeakIssuerOrAudience()
    {
        var tenantId = await FullyReadyTenantAsync();
        var (report, _) = await service.CheckAsync(tenantId, false, CancellationToken.None);
        foreach (var check in report!.Checks)
        {
            Assert.DoesNotContain("https://idp.example.com", check.Reason ?? string.Empty);
            Assert.DoesNotContain("fps-api", check.Reason ?? string.Empty);
        }
    }
}
