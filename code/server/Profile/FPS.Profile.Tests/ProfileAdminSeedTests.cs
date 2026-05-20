using FPS.Profile.Controllers;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Profile.Tests;

public sealed class ProfileAdminSeedTests
{
    private static ProfileAdminController MakeController(IWebHostEnvironment env) =>
        new(new InMemoryProfileRepository(), env);

    private static Mock<IWebHostEnvironment> DevEnv()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        return env;
    }

    private static Mock<IWebHostEnvironment> ProdEnv()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");
        return env;
    }

    private static SeedProfileRequest BasicRequest(string userId, bool hasVehicle = false) =>
        new("tenant-1", userId, true, false, false, false,
            hasVehicle
                ? [new("VEH-01", "ABC123", "Sedan", false, true)]
                : []);

    [Fact]
    public async Task SeedSnapshot_Development_ReturnsNoContent()
    {
        var controller = MakeController(DevEnv().Object);
        var result = await controller.SeedSnapshot(BasicRequest("user-1"));
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task SeedSnapshot_Production_ReturnsNotFound()
    {
        var controller = MakeController(ProdEnv().Object);
        var result = await controller.SeedSnapshot(BasicRequest("user-1"));
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SeedSnapshot_ProfileIsReadableAfterSeed()
    {
        var repo = new InMemoryProfileRepository();
        var controller = new ProfileAdminController(repo, DevEnv().Object);

        await controller.SeedSnapshot(BasicRequest("user-2", hasVehicle: true));

        var profile = await repo.GetAsync("tenant-1", "user-2");
        Assert.NotNull(profile);
        Assert.Equal(ProfileStatus.Active, profile.Status);
        Assert.Single(profile.Vehicles);
        Assert.Equal("ABC123", profile.Vehicles[0].LicensePlate);
    }

    [Fact]
    public async Task SeedSnapshot_MissingUserId_ReturnsBadRequest()
    {
        var controller = MakeController(DevEnv().Object);
        var request = new SeedProfileRequest("tenant-1", "", true, false, false, false, []);
        var result = await controller.SeedSnapshot(request);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SeedSnapshot_IsIdempotent()
    {
        var repo = new InMemoryProfileRepository();
        var controller = new ProfileAdminController(repo, DevEnv().Object);

        await controller.SeedSnapshot(BasicRequest("user-3"));
        await controller.SeedSnapshot(BasicRequest("user-3", hasVehicle: true));

        var profile = await repo.GetAsync("tenant-1", "user-3");
        Assert.Single(profile!.Vehicles);
    }
}
