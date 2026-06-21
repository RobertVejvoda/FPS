using FPS.Profile.Application;
using FPS.Profile.Controllers;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Profile.Tests;

public sealed class HrProfileControllerTests
{
    private readonly Mock<IProfileRepository> repository = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly HrProfileController controller;

    public HrProfileControllerTests()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("hr-1");
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);

        var bootstrapService = new EmployeeBootstrapService(
            repository.Object, new InMemoryDeactivatedUserStore());
        controller = new HrProfileController(repository.Object, bootstrapService, currentUser.Object);
    }

    [Fact]
    public async Task GetRequestorSummary_ProfileExists_Returns200WithSummary()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "abcdef123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProfile(parkingEligible: true, hasCompanyCar: true));

        var result = await controller.GetRequestorSummary("abcdef123456", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<RequestorSummaryResponse>(ok.Value);
        Assert.Equal("abcdef123456", summary.UserId);
        Assert.Equal("Anna Example", summary.DisplayName);
        Assert.Equal("Active", summary.ProfileStatus);
        Assert.True(summary.ParkingEligible);
        Assert.True(summary.HasCompanyCar);
        Assert.Equal(1, summary.ActiveVehicleCount);
        Assert.NotNull(summary.DefaultVehicle);
        Assert.Equal("ABC-123", summary.DefaultVehicle!.LicensePlate);
        Assert.True(summary.DefaultVehicle.IsDefault);
    }

    [Fact]
    public async Task GetRequestorSummary_ShortRef_IsLastSixOfUserIdUppercased()
    {
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProfile(parkingEligible: true));

        var ok = Assert.IsType<OkObjectResult>(
            await controller.GetRequestorSummary("9aaab3ba-1f2e-4dab-9876-1122ff334455", CancellationToken.None));
        var summary = Assert.IsType<RequestorSummaryResponse>(ok.Value);

        // Last 6 hex chars of "9aaab3ba1f2e4dab987611 22ff334455" (after dashes stripped) → "334455"
        Assert.Equal("334455", summary.ShortRef);
    }

    [Fact]
    public async Task GetRequestorSummary_ProfileMissing_Returns404WithShortRef()
    {
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await controller.GetRequestorSummary("aabbccddeeff", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var payload = Assert.IsType<RequestorSummaryNotFound>(notFound.Value);
        Assert.Equal("aabbccddeeff", payload.UserId);
        Assert.Equal("DDEEFF", payload.ShortRef);
    }

    [Fact]
    public async Task GetRequestorSummary_MissingTenant_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await controller.GetRequestorSummary("anything", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetRequestorSummary_NotAuthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await controller.GetRequestorSummary("anything", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetRequestorSummary_BlankUserId_Returns400()
    {
        var result = await controller.GetRequestorSummary("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetRequestorSummary_TenantIsolation_OnlyAuthenticatedTenantQueried()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "abcdef", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProfile(parkingEligible: true));

        await controller.GetRequestorSummary("abcdef", CancellationToken.None);

        repository.Verify(r => r.GetAsync("tenant-1", "abcdef", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.GetAsync(It.Is<string>(t => t != "tenant-1"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetRequestorSummary_NoDefaultVehicleFallsBackToFirstActive()
    {
        var profile = new UserProfile
        {
            TenantId = "tenant-1", UserId = "u-1", Status = ProfileStatus.Active,
            ParkingEligible = true,
            Vehicles =
            [
                new Vehicle("v-1", "FALLBACK-1", "Sedan", false, IsActive: true, IsDefault: false),
                new Vehicle("v-2", "FALLBACK-2", "SUV", false, IsActive: true, IsDefault: false),
            ],
            SnapshotVersion = "v1", FactSource = "admin-seed", UpdatedAt = DateTimeOffset.UtcNow,
        };
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var ok = Assert.IsType<OkObjectResult>(await controller.GetRequestorSummary("u-1", CancellationToken.None));
        var summary = Assert.IsType<RequestorSummaryResponse>(ok.Value);

        Assert.Equal(2, summary.ActiveVehicleCount);
        Assert.NotNull(summary.DefaultVehicle);
        Assert.Equal("FALLBACK-1", summary.DefaultVehicle!.LicensePlate);
        Assert.False(summary.DefaultVehicle.IsDefault);
    }

    [Fact]
    public async Task GetRequestorSummary_NoActiveVehicles_OmitsDefaultVehicle()
    {
        var profile = new UserProfile
        {
            TenantId = "tenant-1", UserId = "u-1", Status = ProfileStatus.Active,
            ParkingEligible = false,
            Vehicles = [new Vehicle("v-1", "OLD-1", "Sedan", false, IsActive: false, IsDefault: true)],
            SnapshotVersion = "v1", FactSource = "admin-seed", UpdatedAt = DateTimeOffset.UtcNow,
        };
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var ok = Assert.IsType<OkObjectResult>(await controller.GetRequestorSummary("u-1", CancellationToken.None));
        var summary = Assert.IsType<RequestorSummaryResponse>(ok.Value);

        Assert.Equal(0, summary.ActiveVehicleCount);
        Assert.Null(summary.DefaultVehicle);
    }

    [Fact]
    public async Task GetRequestorSummary_ReservedSpaceAndAccessibilityFlagsSurfaced()
    {
        repository.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProfile(
                parkingEligible: true,
                accessibilityEligible: true,
                reservedSpaceEligible: true));

        var ok = Assert.IsType<OkObjectResult>(await controller.GetRequestorSummary("u-1", CancellationToken.None));
        var summary = Assert.IsType<RequestorSummaryResponse>(ok.Value);

        Assert.True(summary.AccessibilityEligible);
        Assert.True(summary.ReservedSpaceEligible);
    }

    // Issue #533: company-car-locations endpoint tests. The endpoint only
    // surfaces ACTIVE company-car employees grouped by HomeLocationId; the
    // slot-side warning logic is covered separately by
    // CompanyCarCapacityCalculatorTests in FPS.Configuration.Tests.
    [Fact]
    public async Task GetCompanyCarLocationSummary_GroupsActiveCompanyCarUsersByHomeLocation()
    {
        repository.Setup(r => r.ListByTenantAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>
            {
                BuildCompanyCarProfile("u-a1", homeLocationId: "loc-a"),
                BuildCompanyCarProfile("u-a2", homeLocationId: "loc-a"),
                BuildCompanyCarProfile("u-b1", homeLocationId: "loc-b"),
            });

        var ok = Assert.IsType<OkObjectResult>(
            await controller.GetCompanyCarLocationSummary(CancellationToken.None));
        var payload = Assert.IsType<CompanyCarLocationSummaryResponse>(ok.Value);

        Assert.Equal(2, payload.Locations.Count);

        var rowA = payload.Locations.Single(r => r.LocationId == "loc-a");
        Assert.Equal(2, rowA.CompanyCarEmployeeCount);
        Assert.Equal(new[] { "u-a1", "u-a2" }.OrderBy(x => x), rowA.CompanyCarUserIds.OrderBy(x => x));

        var rowB = payload.Locations.Single(r => r.LocationId == "loc-b");
        Assert.Equal(1, rowB.CompanyCarEmployeeCount);
        Assert.Equal(new[] { "u-b1" }, rowB.CompanyCarUserIds);
    }

    [Fact]
    public async Task GetCompanyCarLocationSummary_ExcludesInactiveProfiles()
    {
        repository.Setup(r => r.ListByTenantAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>
            {
                BuildCompanyCarProfile("u-1", homeLocationId: "loc-a"),
                BuildCompanyCarProfile("u-2", homeLocationId: "loc-a", status: ProfileStatus.Inactive),
                BuildCompanyCarProfile("u-3", homeLocationId: "loc-a", status: ProfileStatus.Suspended),
            });

        var ok = Assert.IsType<OkObjectResult>(
            await controller.GetCompanyCarLocationSummary(CancellationToken.None));
        var payload = Assert.IsType<CompanyCarLocationSummaryResponse>(ok.Value);

        var row = Assert.Single(payload.Locations);
        Assert.Equal(1, row.CompanyCarEmployeeCount);
        Assert.Equal(new[] { "u-1" }, row.CompanyCarUserIds);
    }

    [Fact]
    public async Task GetCompanyCarLocationSummary_ExcludesNonCompanyCarProfiles()
    {
        repository.Setup(r => r.ListByTenantAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>
            {
                BuildCompanyCarProfile("u-cc", homeLocationId: "loc-a"),
                BuildCompanyCarProfile("u-pe", homeLocationId: "loc-a", hasCompanyCar: false),
            });

        var ok = Assert.IsType<OkObjectResult>(
            await controller.GetCompanyCarLocationSummary(CancellationToken.None));
        var payload = Assert.IsType<CompanyCarLocationSummaryResponse>(ok.Value);

        var row = Assert.Single(payload.Locations);
        Assert.Equal(1, row.CompanyCarEmployeeCount);
        Assert.Equal(new[] { "u-cc" }, row.CompanyCarUserIds);
    }

    [Fact]
    public async Task GetCompanyCarLocationSummary_ProfilesWithoutHomeLocation_FallIntoUnassignedBucket()
    {
        repository.Setup(r => r.ListByTenantAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>
            {
                BuildCompanyCarProfile("u-1", homeLocationId: null),
                BuildCompanyCarProfile("u-2", homeLocationId: "loc-a"),
            });

        var ok = Assert.IsType<OkObjectResult>(
            await controller.GetCompanyCarLocationSummary(CancellationToken.None));
        var payload = Assert.IsType<CompanyCarLocationSummaryResponse>(ok.Value);

        Assert.Equal(2, payload.Locations.Count);
        var unassigned = payload.Locations.Single(r => r.LocationId == string.Empty);
        Assert.Equal(1, unassigned.CompanyCarEmployeeCount);
        Assert.Equal(new[] { "u-1" }, unassigned.CompanyCarUserIds);
    }

    [Fact]
    public async Task GetCompanyCarLocationSummary_OnlyQueriesAuthenticatedTenant()
    {
        repository.Setup(r => r.ListByTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

        await controller.GetCompanyCarLocationSummary(CancellationToken.None);

        repository.Verify(r => r.ListByTenantAsync("tenant-1", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.ListByTenantAsync(
            It.Is<string>(t => t != "tenant-1"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCompanyCarLocationSummary_MissingTenant_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await controller.GetCompanyCarLocationSummary(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetCompanyCarLocationSummary_NotAuthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await controller.GetCompanyCarLocationSummary(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetCompanyCarLocationSummary_EmptyTenant_ReturnsEmptyList()
    {
        repository.Setup(r => r.ListByTenantAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProfile>());

        var ok = Assert.IsType<OkObjectResult>(
            await controller.GetCompanyCarLocationSummary(CancellationToken.None));
        var payload = Assert.IsType<CompanyCarLocationSummaryResponse>(ok.Value);

        Assert.Empty(payload.Locations);
    }

    private static UserProfile BuildCompanyCarProfile(
        string userId,
        string? homeLocationId,
        bool hasCompanyCar = true,
        ProfileStatus status = ProfileStatus.Active)
        => new()
        {
            TenantId = "tenant-1",
            UserId = userId,
            Status = status,
            ParkingEligible = true,
            HasCompanyCar = hasCompanyCar,
            HomeLocationId = homeLocationId,
            SnapshotVersion = "v1",
            FactSource = "admin-seed",
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static UserProfile BuildProfile(
        bool parkingEligible,
        bool hasCompanyCar = false,
        bool accessibilityEligible = false,
        bool reservedSpaceEligible = false)
        => new()
        {
            TenantId = "tenant-1",
            UserId = "u-1",
            Status = ProfileStatus.Active,
            ParkingEligible = parkingEligible,
            HasCompanyCar = hasCompanyCar,
            AccessibilityEligible = accessibilityEligible,
            ReservedSpaceEligible = reservedSpaceEligible,
            DisplayName = "Anna Example",
            Vehicles = [new Vehicle("v-1", "ABC-123", "Sedan", false, IsActive: true, IsDefault: true)],
            SnapshotVersion = "v1",
            FactSource = "admin-seed",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
}
