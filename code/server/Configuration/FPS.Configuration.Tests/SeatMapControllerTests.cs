using FPS.Configuration.Application;
using FPS.Configuration.Controllers;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Configuration.Tests;

public sealed class SeatMapControllerTests
{
    private readonly Mock<ICurrentUser> currentUser = new();
    private readonly SeatMapService service;
    private readonly SeatMapController controller;

    public SeatMapControllerTests()
    {
        service = new SeatMapService(
            new InMemorySeatMapRepository(),
            new InMemorySeatBlockRepository(),
            new InMemorySeatMapChangeRepository());

        currentUser.Setup(u => u.TenantId).Returns("tenant-1");
        currentUser.Setup(u => u.UserId).Returns("user-hr");
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);

        controller = new SeatMapController(service, currentUser.Object);
    }

    private async Task SeedMap()
    {
        var map = new SeatMap
        {
            Areas = [new SeatArea { AreaId = "north", TenantId = "tenant-1", LocationId = "GL-HQ", Label = "Team Area North", OwningTeam = "logistics", IsActive = true }],
            Seats =
            [
                new Seat { SeatId = "N-01", TenantId = "tenant-1", LocationId = "GL-HQ", AreaId = "north", Row = 0, Column = 0, Label = "North 01", IsActive = true, IsAccessible = true },
                new Seat { SeatId = "N-02", TenantId = "tenant-1", LocationId = "GL-HQ", AreaId = "north", Row = 0, Column = 1, Label = "North 02", IsActive = true, HasMonitor = true },
            ],
        };
        var errors = await service.ReplaceAsync("tenant-1", "GL-HQ", map, "seed", null, default);
        Assert.Empty(errors);
    }

    // ── PUT seat-map ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSeatMap_Valid_ReturnsNoContent()
    {
        var request = new PutSeatMapRequest(
            [new SeatAreaInputDto("north", "Team Area North", "logistics", true)],
            [new SeatInputDto("N-01", "north", 0, 0, "North 01", true)],
            "initial layout");

        var result = await controller.PutSeatMap("GL-HQ", request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task PutSeatMap_InvalidMap_ReturnsBadRequestWithErrors()
    {
        var request = new PutSeatMapRequest(
            [new SeatAreaInputDto("north", "Team Area North", null, true)],
            [new SeatInputDto("N-01", "no-such-area", 0, 0, "North 01", true)],
            null);

        var result = await controller.PutSeatMap("GL-HQ", request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PutSeatMap_TrimsOwningTeamAndTreatsWhitespaceAsNone()
    {
        var request = new PutSeatMapRequest(
            [new SeatAreaInputDto("open", "Open Area", "   ", true)],
            [],
            null);

        await controller.PutSeatMap("GL-HQ", request, CancellationToken.None);

        var map = await service.GetMapAsync("tenant-1", "GL-HQ", default);
        Assert.Null(map.Areas[0].OwningTeam);
    }

    // ── Blocks ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddSeatBlock_Valid_ReturnsBlockId()
    {
        await SeedMap();
        var request = new AddSeatBlockRequest("N-01", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3), "Maintenance", "cable repair");

        var result = await controller.AddSeatBlock("GL-HQ", request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var blocks = await service.GetBlocksAsync("tenant-1", "GL-HQ", default);
        Assert.Single(blocks);
    }

    [Fact]
    public async Task AddSeatBlock_UnknownReason_ReturnsBadRequest()
    {
        await SeedMap();
        var request = new AddSeatBlockRequest("N-01", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3), "NotAReason", null);

        var result = await controller.AddSeatBlock("GL-HQ", request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RemoveSeatBlock_Unknown_ReturnsNotFound()
    {
        await SeedMap();
        var result = await controller.RemoveSeatBlock("GL-HQ", "missing-block", null, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    // ── Employee-safe map ─────────────────────────────────────────────────────

    [Fact]
    public async Task EmployeeMap_ReturnsGridWithCapabilitiesAndOwningTeam()
    {
        await SeedMap();

        var result = await controller.GetEmployeeSeatMap("GL-HQ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var map = Assert.IsType<EmployeeSeatMapResponse>(ok.Value);
        var area = Assert.Single(map.Areas);
        Assert.Equal("Team Area North", area.Label);
        Assert.Equal("logistics", area.OwningTeam);
        Assert.Equal(2, map.Seats.Count);
        Assert.Contains(map.Seats, s => s.SeatId == "N-01" && s.IsAccessible);
        Assert.Contains(map.Seats, s => s.SeatId == "N-02" && s.HasMonitor);
    }

    [Fact]
    public async Task EmployeeMap_ShowsBlockedRanges_ButRedactsNoteActorAndBlockId()
    {
        await SeedMap();
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        await service.AddBlockAsync(
            "tenant-1", "GL-HQ", "N-01", future, future.AddDays(2),
            SeatBlockReason.Maintenance, "private facilities note about employee X", "user-hr", default);

        var result = await controller.GetEmployeeSeatMap("GL-HQ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var map = Assert.IsType<EmployeeSeatMapResponse>(ok.Value);
        var blocked = Assert.Single(map.Seats.Single(s => s.SeatId == "N-01").Blocks);
        Assert.Equal("Maintenance", blocked.Reason);
        Assert.Equal(future, blocked.FromDate);

        // The employee-safe DTO must not carry the block note, the acting user, or the
        // technical block id — verify at the serialized level so a future field addition
        // cannot silently leak them.
        var json = System.Text.Json.JsonSerializer.Serialize(map);
        Assert.DoesNotContain("private facilities note", json);
        Assert.DoesNotContain("user-hr", json);
        var adminBlocks = await service.GetBlocksAsync("tenant-1", "GL-HQ", default);
        Assert.DoesNotContain(adminBlocks.Single().BlockId, json);
    }

    [Fact]
    public async Task EmployeeMap_OmitsExpiredBlocks()
    {
        await SeedMap();
        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        await service.AddBlockAsync(
            "tenant-1", "GL-HQ", "N-01", past, past.AddDays(2),
            SeatBlockReason.Facilities, null, "user-hr", default);

        var result = await controller.GetEmployeeSeatMap("GL-HQ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var map = Assert.IsType<EmployeeSeatMapResponse>(ok.Value);
        Assert.Empty(map.Seats.Single(s => s.SeatId == "N-01").Blocks);
    }

    // ── Admin view keeps evidence ─────────────────────────────────────────────

    [Fact]
    public async Task AdminSeatMap_IncludesBlockNoteAndActor()
    {
        await SeedMap();
        await service.AddBlockAsync(
            "tenant-1", "GL-HQ", "N-02", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1),
            SeatBlockReason.Reserved, "reserved for visitor day", "user-hr", default);

        var result = await controller.GetSeatMap("GL-HQ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SeatMapResponse>(ok.Value);
        var block = Assert.Single(response.Blocks);
        Assert.Equal("reserved for visitor day", block.Note);
        Assert.Equal("user-hr", block.CreatedByUserId);
    }

    // ── Unauthenticated ───────────────────────────────────────────────────────

    [Fact]
    public async Task Endpoints_WithoutTenantContext_ReturnUnauthorized()
    {
        currentUser.Setup(u => u.TenantId).Returns(string.Empty);

        Assert.IsType<UnauthorizedResult>(await controller.GetSeatMap("GL-HQ", CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.GetEmployeeSeatMap("GL-HQ", CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.PutSeatMap("GL-HQ", new PutSeatMapRequest([], [], null), CancellationToken.None));
    }
}
