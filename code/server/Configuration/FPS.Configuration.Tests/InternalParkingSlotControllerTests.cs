using FPS.Configuration.Application;
using FPS.Configuration.Controllers;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Configuration.Tests;

public sealed class InternalParkingSlotControllerTests
{
    private readonly ParkingSlotService service;
    private readonly InternalParkingSlotController controller;

    public InternalParkingSlotControllerTests()
    {
        service = new ParkingSlotService(new InMemoryParkingSlotRepository(), new InMemorySlotChangeRepository());
        controller = new InternalParkingSlotController(service);
    }

    [Fact]
    public async Task GetSlots_ReturnsFullProjectionIncludingReservedForUserId()
    {
        await Seed("tenant-1", "GL-HQ",
            new ParkingSlot { SlotId = "VIP-01", TenantId = "tenant-1", LocationId = "GL-HQ", IsActive = true, IsCompanyCarOnly = true, ReservedForUserId = "emp-7" },
            new ParkingSlot { SlotId = "A-01", TenantId = "tenant-1", LocationId = "GL-HQ", IsActive = true });

        var result = await controller.GetSlots(new InternalSlotsRequest("tenant-1", "GL-HQ"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var slots = Assert.IsType<List<InternalSlotDto>>(ok.Value);
        Assert.Equal(2, slots.Count);

        var vip = Assert.Single(slots, s => s.SlotId == "VIP-01");
        // Unlike the public /slots/map view, the internal contract carries the owner ID
        // so the draw can honour company-car Tier-1 precedence.
        Assert.Equal("emp-7", vip.ReservedForUserId);
        Assert.True(vip.IsCompanyCarOnly);
    }

    [Fact]
    public async Task GetSlots_ResolvesMotorcycleUnitCount()
    {
        await Seed("tenant-1", "GL-HQ",
            new ParkingSlot { SlotId = "MOTO-01", TenantId = "tenant-1", LocationId = "GL-HQ", IsActive = true, IsMotorcycleCapacity = true },
            new ParkingSlot { SlotId = "A-01", TenantId = "tenant-1", LocationId = "GL-HQ", IsActive = true });

        var result = await controller.GetSlots(new InternalSlotsRequest("tenant-1", "GL-HQ"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var slots = Assert.IsType<List<InternalSlotDto>>(ok.Value);

        var moto = Assert.Single(slots, s => s.SlotId == "MOTO-01");
        Assert.Equal(ParkingSlot.DefaultMotorcycleCapacityUnits, moto.MotorcycleCapacityUnits);
        var ordinary = Assert.Single(slots, s => s.SlotId == "A-01");
        Assert.Equal(1, ordinary.MotorcycleCapacityUnits);
    }

    [Fact]
    public async Task GetSlots_OnlyReturnsRequestedTenant()
    {
        await Seed("tenant-1", "GL-HQ",
            new ParkingSlot { SlotId = "A-01", TenantId = "tenant-1", LocationId = "GL-HQ", IsActive = true });
        await Seed("other", "GL-HQ",
            new ParkingSlot { SlotId = "X-99", TenantId = "other", LocationId = "GL-HQ", IsActive = true });

        var result = await controller.GetSlots(new InternalSlotsRequest("tenant-1", "GL-HQ"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var slots = Assert.IsType<List<InternalSlotDto>>(ok.Value);
        Assert.Equal("A-01", Assert.Single(slots).SlotId);
    }

    [Theory]
    [InlineData("", "GL-HQ")]
    [InlineData("tenant-1", "")]
    public async Task GetSlots_MissingTenantOrLocation_ReturnsBadRequest(string tenantId, string locationId)
    {
        var result = await controller.GetSlots(new InternalSlotsRequest(tenantId, locationId), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private Task Seed(string tenantId, string locationId, params ParkingSlot[] slots) =>
        service.ReplaceAsync(tenantId, locationId, slots, "test-actor", null, default);
}
