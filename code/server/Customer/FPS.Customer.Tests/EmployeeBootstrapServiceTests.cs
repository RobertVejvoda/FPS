using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

public sealed class EmployeeBootstrapServiceTests
{
    private readonly InMemoryTenantRepository tenantRepo = new();
    private readonly InMemoryEmployeeBootstrapRepository repo = new();
    private readonly TenantService tenantService;
    private readonly EmployeeBootstrapService service;

    public EmployeeBootstrapServiceTests()
    {
        tenantService = new TenantService(tenantRepo);
        service = new EmployeeBootstrapService(repo, tenantRepo);
    }

    private async Task<string> CreateTenant(string slug = "acme")
    {
        var (t, _) = await tenantService.CreateAsync(slug, "Corp", "eu", "UTC", [], CancellationToken.None);
        return t!.TenantId;
    }

    private BootstrapEmployeeRequest ValidRequest(string subject = "sub-abc") => new(
        subject, null, true, ["employee"], null, null,
        ParkingEligible: true, HasCompanyCar: false,
        AccessibilityEligible: false, ReservedSpaceEligible: false,
        "admin-entry");

    // ── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_StoresHashedSubject()
    {
        var tenantId = await CreateTenant();

        var (record, error) = await service.RegisterAsync(tenantId, ValidRequest("my-subject"), "actor", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(record);
        Assert.NotEqual("my-subject", record!.ExternalSubjectHash); // must be hashed
        Assert.Equal(tenantId, record.TenantId);
        Assert.True(record.IsActive);
        Assert.Contains("employee", record.FpsRoles);
    }

    [Fact]
    public async Task Register_EmptySubject_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var req = ValidRequest("") with { ExternalSubject = "" };

        var (_, error) = await service.RegisterAsync(tenantId, req, "actor", CancellationToken.None);

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
    public async Task Register_UnknownTenant_ReturnsError()
    {
        var (_, error) = await service.RegisterAsync("no-such-tenant", ValidRequest(), "actor", CancellationToken.None);

        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task Register_TenantIsolation_DifferentTenantsAllowSameSubject()
    {
        var t1 = await CreateTenant("corp-a");
        var t2 = await CreateTenant("corp-b");
        await service.RegisterAsync(t1, ValidRequest("shared-sub"), "actor", CancellationToken.None);

        var (record, error) = await service.RegisterAsync(t2, ValidRequest("shared-sub"), "actor", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(record);
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_ActiveEmployee_SetsInactive()
    {
        var tenantId = await CreateTenant();
        await service.RegisterAsync(tenantId, ValidRequest("sub-d"), "actor", CancellationToken.None);

        var error = await service.DeactivateAsync(tenantId, "sub-d", "actor", CancellationToken.None);

        Assert.Null(error);
        var record = await service.GetAsync(tenantId, "sub-d", CancellationToken.None);
        Assert.False(record!.IsActive);
    }

    [Fact]
    public async Task Deactivate_UnknownEmployee_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await service.DeactivateAsync(tenantId, "no-such-sub", "actor", CancellationToken.None);

        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task Deactivate_EmptySubject_ReturnsError()
    {
        var tenantId = await CreateTenant();
        var error = await service.DeactivateAsync(tenantId, "", "actor", CancellationToken.None);

        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_ValidBatch_ReturnsAcceptedCount()
    {
        var tenantId = await CreateTenant();
        var batch = new List<BootstrapEmployeeRequest>
        {
            ValidRequest("sub-1"), ValidRequest("sub-2"), ValidRequest("sub-3"),
        };

        var summary = await service.ImportAsync(tenantId, batch, "actor", CancellationToken.None);

        Assert.Equal(3, summary.Accepted);
        Assert.Equal(0, summary.Rejected);
        Assert.Empty(summary.Errors);
    }

    [Fact]
    public async Task Import_MixedValidity_ReportsRejectedWithRowNumbers()
    {
        var tenantId = await CreateTenant();
        var batch = new List<BootstrapEmployeeRequest>
        {
            ValidRequest("sub-ok"),
            ValidRequest("") with { ExternalSubject = "" }, // invalid
            ValidRequest("sub-ok"),                         // duplicate
        };

        var summary = await service.ImportAsync(tenantId, batch, "actor", CancellationToken.None);

        Assert.Equal(1, summary.Accepted);
        Assert.Equal(2, summary.Rejected);
        Assert.Contains("Row 2", summary.Errors[0]);
        Assert.Contains("Row 3", summary.Errors[1]);
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
        Assert.Equal(1, summary.ActiveAndEligible); // sub-1 is active+eligible; sub-3 is active but not eligible
    }

    [Fact]
    public async Task Summary_TenantIsolation()
    {
        var t1 = await CreateTenant("corp-a");
        var t2 = await CreateTenant("corp-b");
        await service.RegisterAsync(t1, ValidRequest("sub-a"), "actor", CancellationToken.None);
        await service.RegisterAsync(t1, ValidRequest("sub-b"), "actor", CancellationToken.None);

        var summary = await service.GetSummaryAsync(t2, CancellationToken.None);

        Assert.Equal(0, summary.Total);
    }
}
