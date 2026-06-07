using FPS.Profile.Application;
using FPS.Profile.Controllers;
using FPS.Profile.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Profile.Tests;

public sealed class EmployeeVehicleControllerTests
{
    private readonly Mock<IProfileRepository> repository = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly EmployeeVehicleController controller;

    public EmployeeVehicleControllerTests()
    {
        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("user-1");
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        controller = new EmployeeVehicleController(repository.Object, currentUser.Object);
    }

    private static UserProfile EmptyProfile(IReadOnlyList<Vehicle>? vehicles = null) => new()
    {
        TenantId = "tenant-1",
        UserId = "user-1",
        Status = ProfileStatus.Active,
        Vehicles = vehicles ?? [],
        SnapshotVersion = "1",
        FactSource = "test",
    };

    // ── AddVehicle ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddVehicle_ValidRequest_Returns200WithVehicleId()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile());

        var result = await controller.AddVehicle(new AddVehicleRequest("abc-123", "Sedan", false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = ok.Value!;
        var vehicleId = body.GetType().GetProperty("vehicleId")!.GetValue(body) as string;
        Assert.False(string.IsNullOrEmpty(vehicleId));
    }

    [Fact]
    public async Task AddVehicle_FirstVehicle_IsSetAsDefault()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile());

        UserProfile? saved = null;
        repository.Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p);

        await controller.AddVehicle(new AddVehicleRequest("abc-123", "Sedan", false), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Single(saved!.Vehicles);
        Assert.True(saved.Vehicles[0].IsDefault);
    }

    [Fact]
    public async Task AddVehicle_SecondVehicle_IsNotDefault()
    {
        var existing = new Vehicle("v1", "AAA-111", "Sedan", false, true, IsDefault: true);
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile([existing]));

        UserProfile? saved = null;
        repository.Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p);

        await controller.AddVehicle(new AddVehicleRequest("bbb-222", "SUV", true), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Vehicles.Count);
        Assert.False(saved.Vehicles[1].IsDefault);
        Assert.True(saved.Vehicles[0].IsDefault);
    }

    [Fact]
    public async Task AddVehicle_EmptyLicensePlate_Returns400()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile());

        var result = await controller.AddVehicle(new AddVehicleRequest("", "Sedan", false), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddVehicle_ProfileNotFound_Returns404()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await controller.AddVehicle(new AddVehicleRequest("abc-123", "Sedan", false), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── RemoveVehicle ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveVehicle_ExistingVehicle_Returns204AndDeactivates()
    {
        var vehicle = new Vehicle("v1", "AAA-111", "Sedan", false, true, IsDefault: true);
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile([vehicle]));

        UserProfile? saved = null;
        repository.Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p);

        var result = await controller.RemoveVehicle("v1", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull(saved);
        Assert.False(saved!.Vehicles[0].IsActive);
    }

    [Fact]
    public async Task RemoveVehicle_DefaultRemoved_PromotesNextActiveAsDefault()
    {
        var v1 = new Vehicle("v1", "AAA-111", "Sedan", false, true, IsDefault: true);
        var v2 = new Vehicle("v2", "BBB-222", "SUV", false, true, IsDefault: false);
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile([v1, v2]));

        UserProfile? saved = null;
        repository.Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p);

        await controller.RemoveVehicle("v1", CancellationToken.None);

        Assert.NotNull(saved);
        var remaining = saved!.Vehicles.Where(v => v.IsActive).ToList();
        Assert.Single(remaining);
        Assert.True(remaining[0].IsDefault);
        Assert.Equal("v2", remaining[0].VehicleId);
    }

    [Fact]
    public async Task RemoveVehicle_NotFound_Returns404()
    {
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile());

        var result = await controller.RemoveVehicle("nonexistent", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── SetDefault ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetDefault_ActiveVehicle_ClearsOtherDefaults()
    {
        var v1 = new Vehicle("v1", "AAA-111", "Sedan", false, true, IsDefault: true);
        var v2 = new Vehicle("v2", "BBB-222", "SUV", false, true, IsDefault: false);
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile([v1, v2]));

        UserProfile? saved = null;
        repository.Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p);

        var result = await controller.SetDefault("v2", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull(saved);
        Assert.False(saved!.Vehicles.First(v => v.VehicleId == "v1").IsDefault);
        Assert.True(saved.Vehicles.First(v => v.VehicleId == "v2").IsDefault);
    }

    [Fact]
    public async Task SetDefault_InactiveVehicle_Returns404()
    {
        var vehicle = new Vehicle("v1", "AAA-111", "Sedan", false, IsActive: false, IsDefault: false);
        repository.Setup(r => r.GetAsync("tenant-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyProfile([vehicle]));

        var result = await controller.SetDefault("v1", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
