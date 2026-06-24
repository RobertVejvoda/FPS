using FPS.Configuration.Application;
using FPS.Configuration.Controllers;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace FPS.Configuration.Tests;

public sealed class ConfigDemoSeedControllerTests
{
    private readonly InMemoryParkingSlotRepository slotRepo = new();
    private readonly InMemoryParkingPolicyRepository policyRepo = new();
    private readonly InMemorySlotChangeRepository changeRepo = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly ConfigDemoSeedController controller;

    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private static IConfiguration KeyConfig(string key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("DemoSeed:InternalKey", key)])
            .Build();

    public ConfigDemoSeedControllerTests()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.TenantId).Returns("demo-tenant");
        currentUser.Setup(u => u.UserId).Returns("admin-1");

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-FPS-Seed-Key"] = "test-key";
        var slotService = new ParkingSlotService(slotRepo, changeRepo);
        var policyService = new ParkingPolicyService(policyRepo);
        controller = new ConfigDemoSeedController(slotService, policyService, currentUser.Object, KeyConfig("test-key"));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };
    }

    private ConfigDemoSeedController BuildWithKey(string key, string? headerValue = null)
    {
        var slotService = new ParkingSlotService(slotRepo, changeRepo);
        var policyService = new ParkingPolicyService(policyRepo);
        var c = new ConfigDemoSeedController(slotService, policyService, currentUser.Object, KeyConfig(key));
        var ctx = new DefaultHttpContext();
        if (headerValue is not null)
            ctx.Request.Headers["X-FPS-Seed-Key"] = headerValue;
        c.ControllerContext = new ControllerContext { HttpContext = ctx };
        return c;
    }

    private static ConfigDemoSeedRequest BasicRequest(int slotCount = 5) => new(
        LocationId: "GL-HQ",
        Slots: Enumerable.Range(1, slotCount)
            .Select(i => new DemoSlotSpec($"slot-{i:D3}", IsActive: true,
                HasCharger: false, IsAccessible: false, IsCompanyCarOnly: false,
                IsMotorcycleCapacity: false, ReservedForUserId: null))
            .ToList(),
        Policy: new("Europe/Prague", new TimeOnly(18, 0), 50, 30, 1, 3,
            false, false, false, false, false, 0, [], false, true, "reject"));

    [Fact]
    public async Task DemoSeed_CreatesSlots()
    {
        var result = await controller.DemoSeed(BasicRequest(5), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var slots = await slotRepo.GetByLocationAsync("demo-tenant", "GL-HQ", CancellationToken.None);
        Assert.Equal(5, slots.Count);
    }

    [Fact]
    public async Task DemoSeed_SetsPolicy()
    {
        await controller.DemoSeed(BasicRequest(1), CancellationToken.None);

        var policy = await policyRepo.GetTenantDefaultAsync("demo-tenant", CancellationToken.None);
        Assert.NotNull(policy);
        Assert.Equal("Europe/Prague", policy.TimeZone);
        Assert.Equal(50, policy.DailyRequestCap);
        Assert.True(policy.CompanyCarTier1Enabled);
        Assert.Equal("demo-seed", policy.PublicationReason);
    }

    [Fact]
    public async Task DemoSeed_IsIdempotent_SlotsReplaced()
    {
        await controller.DemoSeed(BasicRequest(10), CancellationToken.None);
        await controller.DemoSeed(BasicRequest(5), CancellationToken.None);

        var slots = await slotRepo.GetByLocationAsync("demo-tenant", "GL-HQ", CancellationToken.None);
        Assert.Equal(5, slots.Count);
    }

    [Fact]
    public async Task DemoSeed_CompanyCarReservedSlot_Stored()
    {
        var request = new ConfigDemoSeedRequest(
            LocationId: "GL-HQ",
            Slots:
            [
                new("slot-cc", IsActive: true, HasCharger: false, IsAccessible: false,
                    IsCompanyCarOnly: true, IsMotorcycleCapacity: false,
                    ReservedForUserId: "a1a10001-0001-0001-0001-000000000001")
            ],
            Policy: new("Europe/Prague", new TimeOnly(18, 0), 50, 30, 1, 3,
                false, false, false, false, false, 0, [], false, true, "reject"));

        await controller.DemoSeed(request, CancellationToken.None);

        var slots = await slotRepo.GetByLocationAsync("demo-tenant", "GL-HQ", CancellationToken.None);
        var ccSlot = Assert.Single(slots, s => s.IsCompanyCarOnly);
        Assert.Equal("a1a10001-0001-0001-0001-000000000001", ccSlot.ReservedForUserId);
    }

    [Fact]
    public async Task DemoSeed_MotorcycleSlot_CapacityUnitsStored()
    {
        var request = new ConfigDemoSeedRequest(
            LocationId: "GL-HQ",
            Slots:
            [
                new("slot-mc", IsActive: true, HasCharger: false, IsAccessible: false,
                    IsCompanyCarOnly: false, IsMotorcycleCapacity: true,
                    ReservedForUserId: null, MotorcycleCapacityUnits: 4)
            ],
            Policy: new("Europe/Prague", new TimeOnly(18, 0), 50, 30, 0, 0,
                false, false, false, false, false, 0, [], false, false, "reject"));

        await controller.DemoSeed(request, CancellationToken.None);

        var slots = await slotRepo.GetByLocationAsync("demo-tenant", "GL-HQ", CancellationToken.None);
        var mcSlot = Assert.Single(slots, s => s.IsMotorcycleCapacity);
        Assert.Equal(4, mcSlot.MotorcycleCapacityUnits);
        Assert.Equal(4, mcSlot.EffectiveMotorcycleCapacityUnits);
    }

    [Fact]
    public async Task DemoSeed_UnauthenticatedUser_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);
        currentUser.Setup(u => u.UserId).Returns(string.Empty);

        var result = await controller.DemoSeed(BasicRequest(1), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task DemoSeed_InternalKeyNotConfigured_Returns503()
    {
        var slotService = new ParkingSlotService(slotRepo, changeRepo);
        var policyService = new ParkingPolicyService(policyRepo);
        var c = new ConfigDemoSeedController(slotService, policyService, currentUser.Object, EmptyConfig());
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
