using FPS.Profile.Application;
using FPS.Profile.Controllers;
using FPS.Profile.Domain;
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

        controller = new HrProfileController(repository.Object, currentUser.Object);
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
