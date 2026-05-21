using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Profile;

namespace FPS.Customer.Tests;

// Captures UpsertAsync calls for assertion in tests.
internal sealed class CapturingProfileSink : IProfileBootstrapSink
{
    public List<(string tenantId, string subjectHash, bool isActive, bool parkingEligible)> Calls = [];

    public Task UpsertAsync(string tenantId, string subjectHash, bool isActive,
        bool parkingEligible, bool hasCompanyCar, bool accessibilityEligible, bool reservedSpaceEligible,
        string factSource, CancellationToken ct)
    {
        Calls.Add((tenantId, subjectHash, isActive, parkingEligible));
        return Task.CompletedTask;
    }
}

public sealed class EmployeeBootstrapServiceTests
{
    private readonly InMemoryTenantRepository tenantRepo = new();
    private readonly InMemoryEmployeeBootstrapRepository repo = new();
    private readonly InMemoryDeactivatedUserStore deactivatedStore = new();
    private readonly CapturingProfileSink profileSink = new();
    private readonly TenantService tenantService;
    private readonly EmployeeBootstrapService service;

    public EmployeeBootstrapServiceTests()
    {
        tenantService = new TenantService(tenantRepo);
        service = new EmployeeBootstrapService(repo, tenantRepo, profileSink, deactivatedStore);
    }

    private async Task<string> CreateTenant(string slug = "acme")
    {
        var (t, _) = await tenantService.CreateAsync(slug, "Corp", "eu", "UTC", [], CancellationToken.None);
        return t!.TenantId;
    }

    private BootstrapEmployeeRequest ValidRequest(string subject = "sub-abc") => new(
        subject, null, true, ["employee"], null, null,
        ParkingEligible: true, HasCompanyCar: false,
        AccessibilityEligible: false, ReservedSpaceEligible: false, "admin-entry");

    // ── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_StoresHashedSubjectAndSyncsProfile()
    {
        var tenantId = await CreateTenant();

        var (record, error) = await service.RegisterAsync(tenantId, ValidRequest("my-subject"), "actor", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(record);
        Assert.NotEqual("my-subject", record!.ExternalSubjectHash);
        Assert.Single(profileSink.Calls);
        Assert.Equal(tenantId, profileSink.Calls[0].tenantId);
        Assert.True(profileSink.Calls[0].parkingEligible);
    }

    [Fact]
    public async Task Register_InactiveEmployee_AddedToDeactivatedStore()
    {
        var tenantId = await CreateTenant();
        var req = ValidRequest() with { IsActive = false };

        await service.RegisterAsync(tenantId, req, "actor", CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("sub-abc");
        Assert.True(deactivatedStore.IsDeactivated(tenantId, hash));
    }

    [Fact]
    public async Task Register_ActiveEmployee_NotInDeactivatedStore()
    {
        var tenantId = await CreateTenant();

        await service.RegisterAsync(tenantId, ValidRequest(), "actor", CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("sub-abc");
        Assert.False(deactivatedStore.IsDeactivated(tenantId, hash));
    }

    [Fact]
    public async Task Register_EmptySubject_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var (_, error) = await service.RegisterAsync(tenantId, ValidRequest(""), "actor", CancellationToken.None);
        Assert.Contains("ExternalSubject", error);
    }

    [Fact]
    public async Task Register_UnknownFpsRole_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var req = ValidRequest() with { FpsRoles = ["employee", "super_power"] };
        var (_, error) = await service.RegisterAsync(tenantId, req, "actor", CancellationToken.None);
        Assert.Contains("super_power", error);
    }

    [Fact]
    public async Task Register_DuplicateSubject_ReturnsError()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest("sub-x"), "actor", CancellationToken.None);
        var (_, error) = await service.RegisterAsync(tenantId, ValidRequest("sub-x"), "actor", CancellationToken.None);
        Assert.Contains("already registered", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_DuplicateEmployeeId_ReturnsError()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest("sub-a") with { EmployeeId = "EMP-001" }, "actor", CancellationToken.None);
        var (_, error) = await service.RegisterAsync(tenantId, ValidRequest("sub-b") with { EmployeeId = "EMP-001" }, "actor", CancellationToken.None);
        Assert.Contains("EMP-001", error);
    }

    [Fact]
    public async Task Register_ArchivedTenant_ReturnsError()
    {
        var tenantId = await CreateTenant("arch");
        await tenantService.TransitionAsync(tenantId, TenantLifecycleState.Archived, "actor", null, null, CancellationToken.None);
        var (_, error) = await service.RegisterAsync(tenantId, ValidRequest(), "actor", CancellationToken.None);
        Assert.Contains("archived", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_TenantIsolation_SameSubjectAllowedAcrossTenants()
    {
        var t1 = await CreateTenant("corp-a");
        var t2 = await CreateTenant("corp-b");
        await service.RegisterAsync(t1, ValidRequest("shared-sub"), "actor", CancellationToken.None);
        var (record, error) = await service.RegisterAsync(t2, ValidRequest("shared-sub"), "actor", CancellationToken.None);
        Assert.Null(error);
        Assert.NotNull(record);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesEligibilityAndSyncsProfile()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest(), "actor", CancellationToken.None);

        var updateReq = new UpdateEmployeeRequest(true, ["employee", "hr_manager"],
            "newemail@corp.com", "loc-B", false, true, true, false);
        var error = await service.UpdateAsync(tenantId, "sub-abc", updateReq, "actor", CancellationToken.None);

        Assert.Null(error);
        var record = await service.GetAsync(tenantId, "sub-abc", CancellationToken.None);
        Assert.Contains("hr_manager", record!.FpsRoles);
        Assert.False(record.ParkingEligible);
        Assert.True(record.HasCompanyCar);
        Assert.Equal("newemail@corp.com", record.NotificationAddress);
        Assert.Equal(2, profileSink.Calls.Count); // register + update
    }

    [Fact]
    public async Task Update_DeactivatesUser_SyncsToDeactivatedStore()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest(), "actor", CancellationToken.None);

        var updateReq = new UpdateEmployeeRequest(false, ["employee"], null, null, true, false, false, false);
        await service.UpdateAsync(tenantId, "sub-abc", updateReq, "actor", CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("sub-abc");
        Assert.True(deactivatedStore.IsDeactivated(tenantId, hash));
    }

    [Fact]
    public async Task Update_ReactivatesUser_RemovesFromDeactivatedStore()
    {
        var tenantId = await CreateTenant();
        var req = ValidRequest() with { IsActive = false };
        await service.RegisterAsync(tenantId, req, "actor", CancellationToken.None);

        var updateReq = new UpdateEmployeeRequest(true, ["employee"], null, null, true, false, false, false);
        await service.UpdateAsync(tenantId, "sub-abc", updateReq, "actor", CancellationToken.None);

        var hash = EmployeeBootstrapService.Hash("sub-abc");
        Assert.False(deactivatedStore.IsDeactivated(tenantId, hash));
    }

    [Fact]
    public async Task Update_UnknownEmployee_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var updateReq = new UpdateEmployeeRequest(true, ["employee"], null, null, true, false, false, false);
        var error = await service.UpdateAsync(tenantId, "no-such-sub", updateReq, "actor", CancellationToken.None);
        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task Update_UnknownRole_ReturnsError()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest(), "actor", CancellationToken.None);
        var updateReq = new UpdateEmployeeRequest(true, ["bad_role"], null, null, true, false, false, false);
        var error = await service.UpdateAsync(tenantId, "sub-abc", updateReq, "actor", CancellationToken.None);
        Assert.Contains("bad_role", error);
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_ActiveEmployee_SetsInactiveAndDeactivatesInStore()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest("sub-d"), "actor", CancellationToken.None);

        var error = await service.DeactivateAsync(tenantId, "sub-d", "actor", CancellationToken.None);

        Assert.Null(error);
        var record = await service.GetAsync(tenantId, "sub-d", CancellationToken.None);
        Assert.False(record!.IsActive);
        Assert.True(deactivatedStore.IsDeactivated(tenantId, EmployeeBootstrapService.Hash("sub-d")));
    }

    [Fact]
    public async Task Deactivate_UnknownEmployee_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await service.DeactivateAsync(tenantId, "no-such", "actor", CancellationToken.None);
        Assert.Contains("not found", error);
    }

    // ── Import — non-partial commit ───────────────────────────────────────────

    [Fact]
    public async Task Import_ValidBatch_CommitsAllRows()
    {
        var tenantId = await CreateTenant();
        var batch = new List<BootstrapEmployeeRequest>
        {
            ValidRequest("sub-1"), ValidRequest("sub-2"), ValidRequest("sub-3"),
        };

        var summary = await service.ImportAsync(tenantId, batch, "actor", CancellationToken.None);

        Assert.Equal(3, summary.Accepted);
        Assert.Equal(0, summary.Rejected);
        Assert.Equal(3, profileSink.Calls.Count);
    }

    [Fact]
    public async Task Import_FirstRowValidSecondInvalid_FirstRowStillCommitted()
    {
        var tenantId = await CreateTenant();
        var batch = new List<BootstrapEmployeeRequest>
        {
            ValidRequest("sub-ok"),
            ValidRequest("") with { ExternalSubject = "" }, // invalid
        };

        var summary = await service.ImportAsync(tenantId, batch, "actor", CancellationToken.None);

        Assert.Equal(1, summary.Accepted);
        Assert.Equal(1, summary.Rejected);
        // sub-ok was saved despite row 2 being invalid
        Assert.NotNull(await service.GetAsync(tenantId, "sub-ok", CancellationToken.None));
    }

    [Fact]
    public async Task Import_DuplicateWithinBatch_RejectsSecondOccurrence()
    {
        var tenantId = await CreateTenant();
        var batch = new List<BootstrapEmployeeRequest>
        {
            ValidRequest("sub-dup"), ValidRequest("sub-dup"),
        };

        var summary = await service.ImportAsync(tenantId, batch, "actor", CancellationToken.None);

        Assert.Equal(1, summary.Accepted);
        Assert.Equal(1, summary.Rejected);
        Assert.Contains("Row 2", summary.Errors[0]);
        Assert.Contains("duplicate", summary.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Import_DuplicateAgainstExistingData_Rejected()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest("existing-sub"), "actor", CancellationToken.None);

        var batch = new List<BootstrapEmployeeRequest> { ValidRequest("existing-sub") };
        var summary = await service.ImportAsync(tenantId, batch, "actor", CancellationToken.None);

        Assert.Equal(0, summary.Accepted);
        Assert.Equal(1, summary.Rejected);
    }

    // ── Profile sink ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProfileSink_UpsertCalledWithCorrectFacts()
    {
        var tenantId = await CreateTenant();
        var req = ValidRequest() with { ParkingEligible = false, HasCompanyCar = true };

        await service.RegisterAsync(tenantId, req, "actor", CancellationToken.None);

        var call = profileSink.Calls.Single();
        Assert.Equal(tenantId, call.tenantId);
        Assert.False(call.parkingEligible);
        Assert.True(call.isActive);
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_ReturnsCorrectCounts()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest("sub-1"), "actor", CancellationToken.None);
        await service.RegisterAsync(tenantId, ValidRequest("sub-2") with { IsActive = false }, "actor", CancellationToken.None);
        await service.RegisterAsync(tenantId, ValidRequest("sub-3") with { ParkingEligible = false }, "actor", CancellationToken.None);

        var summary = await service.GetSummaryAsync(tenantId, CancellationToken.None);

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.Active);
        Assert.Equal(1, summary.Inactive);
        Assert.Equal(1, summary.ActiveAndEligible);
    }

    [Fact]
    public async Task Summary_TenantIsolation()
    {
        var t1 = await CreateTenant("corp-a");
        var t2 = await CreateTenant("corp-b");
        await service.RegisterAsync(t1, ValidRequest("sub-a"), "actor", CancellationToken.None);

        var summary = await service.GetSummaryAsync(t2, CancellationToken.None);
        Assert.Equal(0, summary.Total);
    }
}
