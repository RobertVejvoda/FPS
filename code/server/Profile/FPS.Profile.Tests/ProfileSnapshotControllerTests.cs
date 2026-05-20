using FPS.Profile.Application;
using FPS.Profile.Controllers;
using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Profile;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Profile.Tests;

public sealed class ProfileSnapshotControllerTests
{
    private readonly Mock<IProfileRepository> repository = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly ProfileSnapshotController controller;

    public ProfileSnapshotControllerTests()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("user-1");
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);

        controller = new ProfileSnapshotController(repository.Object, currentUser.Object);
    }

    [Fact]
    public async Task GetSnapshot_ActiveEligibleProfile_Returns200WithSnapshot()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProfile(ProfileStatus.Active, parkingEligible: true));

        var result = await controller.GetSnapshot(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var snapshot = Assert.IsType<ProfileSnapshot>(ok.Value);
        Assert.Equal("tenant-1", snapshot.TenantId);
        Assert.Equal("Active", snapshot.ProfileStatus);
        Assert.True(snapshot.ParkingEligible);
    }

    [Fact]
    public async Task GetSnapshot_ProfileNotFound_ProvisionsSsoProfileAndReturns200()
    {
        // SSO-first: first access provisions a minimal profile from authenticated claims.
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await controller.GetSnapshot(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var snapshot = Assert.IsType<ProfileSnapshot>(ok.Value);
        Assert.Equal("tenant-1", snapshot.TenantId);
        Assert.Equal("user-1", snapshot.UserId);
        Assert.Equal("Active", snapshot.ProfileStatus);
    }

    [Fact]
    public async Task GetSnapshot_SsoProvisioning_SavesProfileWithSsoFactSource()
    {
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        UserProfile? saved = null;
        repository.Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p)
            .Returns(Task.CompletedTask);

        await controller.GetSnapshot(CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal("sso-claims", saved!.FactSource);
        Assert.Equal("tenant-1", saved.TenantId);
        Assert.Equal("user-1", saved.UserId);
        Assert.Equal(ProfileStatus.Active, saved.Status);
    }

    [Fact]
    public async Task GetSnapshot_SsoProvisioning_DefaultsAllEligibilityToFalse()
    {
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        await controller.GetSnapshot(CancellationToken.None);

        repository.Verify(r => r.SaveAsync(
            It.Is<UserProfile>(p =>
                !p.ParkingEligible &&
                !p.HasCompanyCar &&
                !p.AccessibilityEligible &&
                !p.ReservedSpaceEligible &&
                p.Vehicles.Count == 0),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSnapshot_TenantIsolation_UsesOnlyClaimsContext()
    {
        // Profile is only fetched for the authenticated (tenantId, userId) pair —
        // never from a caller-supplied request parameter.
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProfile(ProfileStatus.Active, parkingEligible: true));

        var result = await controller.GetSnapshot(CancellationToken.None);

        var snapshot = Assert.IsType<ProfileSnapshot>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("tenant-1", snapshot.TenantId);
        Assert.Equal("user-1", snapshot.UserId);

        // No cross-tenant lookup was made
        repository.Verify(r => r.GetAsync("other-tenant", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetSnapshot_MissingTenantId_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await controller.GetSnapshot(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetSnapshot_MissingUserId_Returns401()
    {
        currentUser.Setup(u => u.UserId).Returns(string.Empty);

        var result = await controller.GetSnapshot(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetSnapshot_OnlyActiveVehiclesReturned()
    {
        var profile = BuildProfile(ProfileStatus.Active, parkingEligible: true, includeInactiveVehicle: true);
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await controller.GetSnapshot(CancellationToken.None);

        var snapshot = Assert.IsType<ProfileSnapshot>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.All(snapshot.Vehicles, v => Assert.True(v.IsActive));
    }

    [Fact]
    public async Task GetSnapshot_CompanyCar_ReflectedInSnapshot()
    {
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProfile(ProfileStatus.Active, parkingEligible: true, hasCompanyCar: true));

        var result = await controller.GetSnapshot(CancellationToken.None);

        var snapshot = Assert.IsType<ProfileSnapshot>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(snapshot.HasCompanyCar);
    }

    private static UserProfile BuildProfile(
        ProfileStatus status, bool parkingEligible,
        bool hasCompanyCar = false, bool includeInactiveVehicle = false)
    {
        var vehicles = new List<Vehicle>
        {
            new("v-1", "ABC-123", "Sedan", false, true)
        };
        if (includeInactiveVehicle)
            vehicles.Add(new Vehicle("v-2", "OLD-111", "Sedan", false, false));

        return new UserProfile
        {
            TenantId = "tenant-1",
            UserId = "user-1",
            Status = status,
            ParkingEligible = parkingEligible,
            HasCompanyCar = hasCompanyCar,
            AccessibilityEligible = false,
            ReservedSpaceEligible = false,
            Vehicles = vehicles,
            SnapshotVersion = "v1",
            FactSource = "admin-seed",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
