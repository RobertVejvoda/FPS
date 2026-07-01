using FPS.Configuration.Application;
using FPS.Configuration.Controllers;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Configuration.Tests;

public sealed class ConfigDemoSeedControllerTests
{
    private readonly InMemoryParkingSlotRepository slotRepo = new();
    private readonly InMemoryParkingPolicyRepository policyRepo = new();
    private readonly InMemorySlotChangeRepository changeRepo = new();
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly ConfigDemoSeedController controller;

    public ConfigDemoSeedControllerTests()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.UserId).Returns("admin-1");

        var slotService = new ParkingSlotService(slotRepo, changeRepo);
        var policyService = new ParkingPolicyService(policyRepo);
        controller = new ConfigDemoSeedController(slotService, policyService, currentUser.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    private static ConfigDemoSeedRequest BasicRequest(int slotCount = 5) => new(
        TenantId: "demo-tenant",
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
            TenantId: "demo-tenant",
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
            TenantId: "demo-tenant",
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
    public async Task DemoSeed_MissingTenantId_Returns400()
    {
        var request = BasicRequest(1) with { TenantId = "" };

        var result = await controller.DemoSeed(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DemoSeed_MalformedTenantId_Returns400()
    {
        // A tenant id that fails TenantStorageKey.Sanitise must 400 at the boundary, not bubble a 500.
        var request = BasicRequest(1) with { TenantId = "Bad Tenant!" };

        var result = await controller.DemoSeed(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Controller_IsDaprInternalOnly()
    {
        // The internal boundary: only Dapr-delivered traffic (dapr-api-token) reaches this endpoint;
        // gateway-routed external callers cannot, so key-only anonymous access is no longer possible.
        var attribute = Attribute.GetCustomAttribute(
            typeof(ConfigDemoSeedController), typeof(DaprInternalOnlyAttribute));

        Assert.NotNull(attribute);
    }
}
