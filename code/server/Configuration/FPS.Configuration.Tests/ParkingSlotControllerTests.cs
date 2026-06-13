using FPS.Configuration.Application;
using FPS.Configuration.Controllers;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Configuration.Tests;

public sealed class ParkingSlotControllerTests
{
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly ParkingSlotService service;
    private readonly InMemoryParkingSlotRepository slotRepo = new();
    private readonly ParkingSlotController controller;

    public ParkingSlotControllerTests()
    {
        service = new ParkingSlotService(slotRepo, new InMemorySlotChangeRepository());

        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("user-emp");
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);

        controller = new ParkingSlotController(service, currentUser.Object);
    }

    [Fact]
    public async Task GetSlotsMap_ReturnsPublicSafeProjection()
    {
        await SeedSlots(
            new ParkingSlot { SlotId = "101", TenantId = "tenant-1", LocationId = "Prague", IsActive = true, HasCharger = true },
            new ParkingSlot { SlotId = "201", TenantId = "tenant-1", LocationId = "Prague", IsActive = true, IsAccessible = true });

        var result = await controller.GetSlotsMap("Prague", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var map = Assert.IsType<List<SlotMapDto>>(ok.Value);
        Assert.Equal(2, map.Count);
        Assert.Contains(map, s => s.SlotId == "101" && s.HasCharger);
        Assert.Contains(map, s => s.SlotId == "201" && s.IsAccessible);
    }

    [Fact]
    public async Task GetSlotsMap_RedactsReservedForUserId()
    {
        await SeedSlots(
            new ParkingSlot
            {
                SlotId = "311", TenantId = "tenant-1", LocationId = "Prague",
                IsActive = true, ReservedForUserId = "secret-employee-ref-abc123"
            });

        var result = await controller.GetSlotsMap("Prague", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var map = Assert.IsType<List<SlotMapDto>>(ok.Value);
        var slot = Assert.Single(map);
        Assert.True(slot.IsReserved);

        // Critical: nothing on the public DTO carries the reservation owner ID.
        var dtoFields = string.Join(",",
            typeof(SlotMapDto).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("Reservedfor", dtoFields, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UserId", dtoFields);
    }

    [Fact]
    public async Task GetSlotsMap_NoReservation_IsReservedFalse()
    {
        await SeedSlots(new ParkingSlot
        {
            SlotId = "101", TenantId = "tenant-1", LocationId = "Prague",
            IsActive = true, ReservedForUserId = null
        });

        var result = await controller.GetSlotsMap("Prague", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var map = Assert.IsType<List<SlotMapDto>>(ok.Value);
        Assert.False(map[0].IsReserved);
    }

    [Fact]
    public async Task GetSlotsMap_NotAuthenticated_Returns401()
    {
        currentUser.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await controller.GetSlotsMap("Prague", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetSlotsMap_MissingTenant_Returns401()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        var result = await controller.GetSlotsMap("Prague", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetSlotsMap_OnlyReturnsCurrentTenantSlots()
    {
        await SeedSlots(
            new ParkingSlot { SlotId = "111", TenantId = "tenant-1", LocationId = "Prague", IsActive = true });

        var otherTenantSlots = new[]
        {
            new ParkingSlot { SlotId = "999", TenantId = "other-tenant", LocationId = "Prague", IsActive = true }
        };
        await service.ReplaceAsync("other-tenant", "Prague", otherTenantSlots, "test-actor", null, default);

        var result = await controller.GetSlotsMap("Prague", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var map = Assert.IsType<List<SlotMapDto>>(ok.Value);
        Assert.Single(map);
        Assert.Equal("111", map[0].SlotId);
    }

    [Fact]
    public async Task GetSlotsMap_EmptyLocation_ReturnsEmptyList()
    {
        var result = await controller.GetSlotsMap("Empty", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var map = Assert.IsType<List<SlotMapDto>>(ok.Value);
        Assert.Empty(map);
    }

    private async Task SeedSlots(params ParkingSlot[] slots) =>
        await service.ReplaceAsync("tenant-1", slots[0].LocationId, slots, "test-actor", null, default);
}
