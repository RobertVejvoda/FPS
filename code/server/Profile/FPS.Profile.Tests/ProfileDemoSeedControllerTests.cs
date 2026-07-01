using FPS.Profile.Controllers;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FPS.Profile.Tests;

public sealed class ProfileDemoSeedControllerTests
{
    private const string Tenant = "demo-tenant";
    private readonly InMemoryProfileRepository repository = new();
    private readonly InMemoryDeactivatedUserStore deactivatedStore = new();
    private readonly ProfileDemoSeedController controller;

    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private static IConfiguration KeyConfig(string key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("DemoSeed:InternalKey", key)])
            .Build();

    public ProfileDemoSeedControllerTests()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-FPS-Seed-Key"] = "test-key";
        controller = new ProfileDemoSeedController(repository, deactivatedStore, KeyConfig("test-key"));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };
    }

    private ProfileDemoSeedController BuildWithKey(string key, string? headerValue = null)
    {
        var c = new ProfileDemoSeedController(repository, deactivatedStore, KeyConfig(key));
        var ctx = new DefaultHttpContext();
        if (headerValue is not null)
            ctx.Request.Headers["X-FPS-Seed-Key"] = headerValue;
        c.ControllerContext = new ControllerContext { HttpContext = ctx };
        return c;
    }

    private static ProfileDemoSeedRequest BasicRequest(int count = 1)
    {
        var employees = Enumerable.Range(1, count).Select(i => new DemoEmployeeSpec(
            UserId: $"user-{i:D4}",
            DisplayName: $"Employee {i}",
            FpsRoles: ["employee"],
            NotificationAddress: null,
            HomeLocationId: "HQ",
            ParkingEligible: true,
            HasCompanyCar: false,
            AccessibilityEligible: false,
            ReservedSpaceEligible: false,
            Vehicles: [new($"veh-{i:D4}", $"PL{i:D4}AA", "car", IsElectric: false, IsDefault: true)]
        )).ToList();
        return new ProfileDemoSeedRequest(Tenant, employees);
    }

    [Fact]
    public async Task DemoSeed_CreatesProfiles()
    {
        var result = await controller.DemoSeed(BasicRequest(3), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var profile = await repository.GetAsync(Tenant, "user-0001");
        Assert.NotNull(profile);
        Assert.Equal("Employee 1", profile.DisplayName);
        Assert.Equal("demo-seed", profile.FactSource);
    }

    [Fact]
    public async Task DemoSeed_ReturnsCorrectCount()
    {
        var result = await controller.DemoSeed(BasicRequest(5), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("5", json);
    }

    [Fact]
    public async Task DemoSeed_SetsCorrectFactSource()
    {
        await controller.DemoSeed(BasicRequest(1), CancellationToken.None);

        var profile = await repository.GetAsync(Tenant, "user-0001");
        Assert.Equal("demo-seed", profile!.FactSource);
        Assert.Equal("demo-seed-v1", profile.SnapshotVersion);
    }

    [Fact]
    public async Task DemoSeed_IsIdempotent()
    {
        await controller.DemoSeed(BasicRequest(1), CancellationToken.None);
        await controller.DemoSeed(BasicRequest(1), CancellationToken.None);

        var profile = await repository.GetAsync(Tenant, "user-0001");
        Assert.NotNull(profile);
        Assert.Equal("Employee 1", profile.DisplayName);
    }

    [Fact]
    public async Task DemoSeed_WithVehicles_VehiclesStored()
    {
        var request = new ProfileDemoSeedRequest(Tenant,
        [
            new("cc-user", "Alice", ["employee"], null, "HQ", true, true, false, false,
            [
                new("veh-cc1", "3GL-AA01", "car", IsElectric: false, IsDefault: true)
            ])
        ]);

        await controller.DemoSeed(request, CancellationToken.None);

        var profile = await repository.GetAsync(Tenant, "cc-user");
        Assert.NotNull(profile);
        Assert.True(profile.HasCompanyCar);
        Assert.Single(profile.Vehicles);
        Assert.Equal("3GL-AA01", profile.Vehicles[0].LicensePlate);
        Assert.True(profile.Vehicles[0].IsDefault);
    }

    [Fact]
    public async Task DemoSeed_MultipleVehicles_NormalizesDefault()
    {
        var request = new ProfileDemoSeedRequest(Tenant,
        [
            new("mc-user", "David", ["employee"], null, "HQ", true, false, false, false,
            [
                new("veh-car", "3GL-DD04", "car",        IsElectric: false, IsDefault: true),
                new("veh-mc",  "3GL-DD05", "motorcycle", IsElectric: false, IsDefault: false),
            ])
        ]);

        await controller.DemoSeed(request, CancellationToken.None);

        var profile = await repository.GetAsync(Tenant, "mc-user");
        Assert.Equal(2, profile!.Vehicles.Count);
        Assert.Single(profile.Vehicles, v => v.IsDefault);
        Assert.Equal("veh-car", profile.Vehicles.Single(v => v.IsDefault).VehicleId);
    }

    [Fact]
    public async Task DemoSeed_NoVehicles_ProfileStillCreated()
    {
        var request = new ProfileDemoSeedRequest(Tenant,
        [
            new("no-car-user", "Gabi", ["employee"], null, "HQ", false, false, false, false, [])
        ]);

        await controller.DemoSeed(request, CancellationToken.None);

        var profile = await repository.GetAsync(Tenant, "no-car-user");
        Assert.NotNull(profile);
        Assert.Empty(profile.Vehicles);
        Assert.False(profile.ParkingEligible);
    }

    [Fact]
    public async Task DemoSeed_NoJwt_ValidKeyAndBodyTenant_Succeeds()
    {
        // PLAT003C-C2: no operator JWT is required — a valid seed key plus a body tenant id
        // authorizes the endpoint (the scheduled-reset reseed path).
        var result = await controller.DemoSeed(BasicRequest(1), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(await repository.GetAsync(Tenant, "user-0001"));
    }

    [Fact]
    public async Task DemoSeed_MissingTenantId_Returns400()
    {
        var result = await controller.DemoSeed(BasicRequest(1) with { TenantId = "" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DemoSeed_InternalKeyNotConfigured_Returns503()
    {
        var c = new ProfileDemoSeedController(repository, deactivatedStore, EmptyConfig());
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await c.DemoSeed(BasicRequest(1), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
    }

    [Fact]
    public async Task DemoSeed_InternalKeyConfigured_MissingHeader_Returns401()
    {
        var c = BuildWithKey("secret-key", headerValue: null);

        var result = await c.DemoSeed(BasicRequest(1), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task DemoSeed_InternalKeyConfigured_WrongHeader_Returns401()
    {
        var c = BuildWithKey("secret-key", headerValue: "wrong-key");

        var result = await c.DemoSeed(BasicRequest(1), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task DemoSeed_InternalKeyConfigured_CorrectHeader_Succeeds()
    {
        var c = BuildWithKey("secret-key", headerValue: "secret-key");

        var result = await c.DemoSeed(BasicRequest(1), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }
}
