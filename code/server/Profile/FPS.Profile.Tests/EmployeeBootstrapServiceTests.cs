using FPS.Profile.Application;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Identity;

namespace FPS.Profile.Tests;

public sealed class EmployeeBootstrapServiceTests
{
    private readonly InMemoryProfileRepository profileRepo = new();
    private readonly InMemoryDeactivatedUserStore deactivatedStore = new();
    private readonly EmployeeBootstrapService service;

    public EmployeeBootstrapServiceTests()
    {
        service = new EmployeeBootstrapService(profileRepo, deactivatedStore);
    }

    private BootstrapEmployeeRequest ValidRequest(string subject = "sub-abc") => new(
        subject, null, true, ["employee"], null, null,
        ParkingEligible: true, HasCompanyCar: false,
        AccessibilityEligible: false, ReservedSpaceEligible: false, "admin-entry");

    [Fact]
    public async Task Register_ValidRequest_WritesProfileSnapshot()
    {
        var (profile, error) = await service.RegisterAsync("t1", ValidRequest("sub-1"), CancellationToken.None);
        Assert.Null(error);
        var stored = await profileRepo.GetAsync("t1", profile!.UserId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.True(stored!.ParkingEligible);
        Assert.Equal("admin-entry", stored.FactSource);
    }

    [Fact]
    public async Task Register_InactiveEmployee_ProfileStatusInactiveAndDeactivatedStoreSet()
    {
        var req = ValidRequest() with { IsActive = false };
        var (profile, _) = await service.RegisterAsync("t1", req, CancellationToken.None);
        Assert.Equal(Domain.ProfileStatus.Inactive, profile!.Status);
        Assert.True(deactivatedStore.IsDeactivated("t1", profile.UserId));
    }

    [Fact]
    public async Task Register_ActiveEmployee_DeactivatedStoreNotSet()
    {
        var (profile, _) = await service.RegisterAsync("t1", ValidRequest(), CancellationToken.None);
        Assert.Equal(Domain.ProfileStatus.Active, profile!.Status);
        Assert.False(deactivatedStore.IsDeactivated("t1", profile.UserId));
    }

    [Fact]
    public async Task Register_DuplicateSubject_ReturnsError()
    {
        await service.RegisterAsync("t1", ValidRequest("sub-x"), CancellationToken.None);
        var (_, error) = await service.RegisterAsync("t1", ValidRequest("sub-x"), CancellationToken.None);
        Assert.Contains("already registered", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_EmptySubject_ReturnsError()
    {
        var (_, error) = await service.RegisterAsync("t1", ValidRequest(""), CancellationToken.None);
        Assert.Contains("ExternalSubject", error);
    }

    [Fact]
    public async Task Register_UnknownRole_ReturnsError()
    {
        var req = ValidRequest() with { FpsRoles = ["employee", "overlord"] };
        var (_, error) = await service.RegisterAsync("t1", req, CancellationToken.None);
        Assert.Contains("overlord", error);
    }

    [Fact]
    public async Task Register_TenantIsolation_SameSubjectAllowedInDifferentTenants()
    {
        await service.RegisterAsync("t1", ValidRequest("shared-sub"), CancellationToken.None);
        var (profile, error) = await service.RegisterAsync("t2", ValidRequest("shared-sub"), CancellationToken.None);
        Assert.Null(error);
        Assert.NotNull(profile);
    }

    [Fact]
    public async Task Update_ByStoredHash_UpdatesProfileSnapshot()
    {
        var (profile, _) = await service.RegisterAsync("t1", ValidRequest(), CancellationToken.None);
        var hash = profile!.UserId;
        var updateReq = new UpdateEmployeeRequest(true, ["employee", "hr_manager"],
            null, null, false, true, true, false);
        var error = await service.UpdateAsync("t1", hash, updateReq, CancellationToken.None);
        Assert.Null(error);
        var stored = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        Assert.False(stored!.ParkingEligible);
        Assert.True(stored.HasCompanyCar);
    }

    [Fact]
    public async Task Update_DeactivatesUser_SetsDeactivatedStore()
    {
        var (profile, _) = await service.RegisterAsync("t1", ValidRequest(), CancellationToken.None);
        await service.UpdateAsync("t1", profile!.UserId,
            new UpdateEmployeeRequest(false, ["employee"], null, null, true, false, false, false),
            CancellationToken.None);
        Assert.True(deactivatedStore.IsDeactivated("t1", profile.UserId));
    }

    [Fact]
    public async Task Update_ReactivatesUser_ClearsDeactivatedStore()
    {
        var req = ValidRequest() with { IsActive = false };
        var (profile, _) = await service.RegisterAsync("t1", req, CancellationToken.None);
        await service.UpdateAsync("t1", profile!.UserId,
            new UpdateEmployeeRequest(true, ["employee"], null, null, true, false, false, false),
            CancellationToken.None);
        Assert.False(deactivatedStore.IsDeactivated("t1", profile.UserId));
    }

    [Fact]
    public async Task Update_UnknownHash_ReturnsError()
    {
        var error = await service.UpdateAsync("t1", "nonexistent-hash",
            new UpdateEmployeeRequest(true, ["employee"], null, null, true, false, false, false),
            CancellationToken.None);
        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task Deactivate_ActiveEmployee_SetsInactiveAndDeactivatedStore()
    {
        await service.RegisterAsync("t1", ValidRequest("sub-d"), CancellationToken.None);
        var error = await service.DeactivateAsync("t1", "sub-d", CancellationToken.None);
        Assert.Null(error);
        var hash = EmployeeBootstrapService.Hash("sub-d");
        var stored = await profileRepo.GetAsync("t1", hash, CancellationToken.None);
        Assert.Equal(Domain.ProfileStatus.Inactive, stored!.Status);
        Assert.True(deactivatedStore.IsDeactivated("t1", hash));
    }

    [Fact]
    public async Task Deactivate_UnknownEmployee_ReturnsError()
    {
        var error = await service.DeactivateAsync("t1", "no-such-sub", CancellationToken.None);
        Assert.Contains("not found", error);
    }

    [Fact]
    public async Task Import_ValidBatch_CommitsAll()
    {
        var batch = new[] { ValidRequest("sub-1"), ValidRequest("sub-2"), ValidRequest("sub-3") };
        var summary = await service.ImportAsync("t1", batch, CancellationToken.None);
        Assert.Equal(3, summary.Accepted);
        Assert.Equal(0, summary.Rejected);
        Assert.NotNull(await profileRepo.GetAsync("t1", EmployeeBootstrapService.Hash("sub-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Import_OneInvalidRow_CommitsNone()
    {
        var batch = new[] { ValidRequest("sub-ok"), ValidRequest("") with { ExternalSubject = "" } };
        var summary = await service.ImportAsync("t1", batch, CancellationToken.None);
        Assert.Equal(0, summary.Accepted);
        Assert.Equal(1, summary.Rejected);
        Assert.Null(await profileRepo.GetAsync("t1", EmployeeBootstrapService.Hash("sub-ok"), CancellationToken.None));
    }

    [Fact]
    public async Task Import_DuplicateWithinBatch_CommitsNone()
    {
        var batch = new[] { ValidRequest("sub-dup"), ValidRequest("sub-dup") };
        var summary = await service.ImportAsync("t1", batch, CancellationToken.None);
        Assert.Equal(0, summary.Accepted);
        Assert.Contains("Row 2", summary.Errors[0]);
        Assert.Null(await profileRepo.GetAsync("t1", EmployeeBootstrapService.Hash("sub-dup"), CancellationToken.None));
    }

    [Fact]
    public async Task Import_DuplicateAgainstExisting_CommitsNone()
    {
        await service.RegisterAsync("t1", ValidRequest("existing"), CancellationToken.None);
        var batch = new[] { ValidRequest("new-sub"), ValidRequest("existing") };
        var summary = await service.ImportAsync("t1", batch, CancellationToken.None);
        Assert.Equal(0, summary.Accepted);
        Assert.Null(await profileRepo.GetAsync("t1", EmployeeBootstrapService.Hash("new-sub"), CancellationToken.None));
    }
}
