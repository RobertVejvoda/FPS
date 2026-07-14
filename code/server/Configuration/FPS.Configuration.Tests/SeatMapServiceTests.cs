using FPS.Configuration.Application;
using FPS.Configuration.Domain;
using FPS.Configuration.Infrastructure;

namespace FPS.Configuration.Tests;

public sealed class SeatMapServiceTests
{
    private static SeatArea Area(string areaId, string? owningTeam = null, string label = "Area") =>
        new() { AreaId = areaId, TenantId = "tenant-1", LocationId = "loc-1", Label = label, OwningTeam = owningTeam, IsActive = true };

    private static Seat Seat(string seatId, string areaId, int row = 0, int column = 0, string label = "Seat") =>
        new() { SeatId = seatId, TenantId = "tenant-1", LocationId = "loc-1", AreaId = areaId, Row = row, Column = column, Label = label, IsActive = true };

    private static SeatMapService MakeService(out InMemorySeatMapChangeRepository changeRepo)
    {
        changeRepo = new InMemorySeatMapChangeRepository();
        return new SeatMapService(new InMemorySeatMapRepository(), new InMemorySeatBlockRepository(), changeRepo);
    }

    // ── Replace: happy path ───────────────────────────────────────────────────

    [Fact]
    public async Task Replace_ValidMap_PersistsAndRecordsChange()
    {
        var service = MakeService(out var changeRepo);
        var map = new SeatMap
        {
            Areas = [Area("north", owningTeam: "logistics")],
            Seats = [Seat("N-01", "north", 0, 0), Seat("N-02", "north", 0, 1)],
        };

        var errors = await service.ReplaceAsync("tenant-1", "loc-1", map, "actor-1", "initial layout", default);

        Assert.Empty(errors);
        var stored = await service.GetMapAsync("tenant-1", "loc-1", default);
        Assert.Single(stored.Areas);
        Assert.Equal("logistics", stored.Areas[0].OwningTeam);
        Assert.Equal(2, stored.Seats.Count);

        var history = await changeRepo.GetHistoryAsync("tenant-1", "loc-1", 10, default);
        var change = Assert.Single(history);
        Assert.Equal(SeatMapChangeRecord.TypeMapReplaced, change.ChangeType);
        Assert.Equal("actor-1", change.ChangedByUserId);
        Assert.Equal("initial layout", change.ChangeReason);
        Assert.Equal(1, change.AreaCount);
        Assert.Equal(2, change.SeatCount);
    }

    [Fact]
    public async Task Replace_EmptyMap_IsValid()
    {
        var service = MakeService(out _);
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", new SeatMap(), "actor-1", null, default);
        Assert.Empty(errors);
    }

    // ── Replace: validation ───────────────────────────────────────────────────

    [Fact]
    public async Task Replace_DuplicateAreaId_ReturnsError()
    {
        var service = MakeService(out _);
        var map = new SeatMap { Areas = [Area("north"), Area("north")] };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", map, "a", null, default);
        Assert.Contains(errors, e => e.Contains("north"));
    }

    [Fact]
    public async Task Replace_DuplicateSeatId_ReturnsError()
    {
        var service = MakeService(out _);
        var map = new SeatMap { Areas = [Area("a1")], Seats = [Seat("S1", "a1", 0, 0), Seat("S1", "a1", 1, 1)] };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", map, "a", null, default);
        Assert.Contains(errors, e => e.Contains("Duplicate seatId"));
    }

    [Fact]
    public async Task Replace_SeatWithUnknownArea_ReturnsError()
    {
        var service = MakeService(out _);
        var map = new SeatMap { Areas = [Area("a1")], Seats = [Seat("S1", "no-such-area")] };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", map, "a", null, default);
        Assert.Contains(errors, e => e.Contains("no-such-area"));
    }

    [Fact]
    public async Task Replace_MissingLabels_ReturnsErrors()
    {
        var service = MakeService(out _);
        var map = new SeatMap
        {
            Areas = [Area("a1", label: "")],
            Seats = [Seat("S1", "a1", label: " ")],
        };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", map, "a", null, default);
        Assert.Equal(2, errors.Count(e => e.Contains("label is required")));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 501)]
    public async Task Replace_RowOrColumnOutOfBounds_ReturnsError(int row, int column)
    {
        var service = MakeService(out _);
        var map = new SeatMap { Areas = [Area("a1")], Seats = [Seat("S1", "a1", row, column)] };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", map, "a", null, default);
        Assert.Contains(errors, e => e.Contains("row and column"));
    }

    [Fact]
    public async Task Replace_TwoSeatsOnSamePositionInArea_ReturnsError()
    {
        var service = MakeService(out _);
        var map = new SeatMap { Areas = [Area("a1")], Seats = [Seat("S1", "a1", 2, 3), Seat("S2", "a1", 2, 3)] };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", map, "a", null, default);
        Assert.Contains(errors, e => e.Contains("already occupies"));
    }

    [Fact]
    public async Task Replace_SamePositionInDifferentAreas_IsValid()
    {
        var service = MakeService(out _);
        var map = new SeatMap
        {
            Areas = [Area("a1"), Area("a2")],
            Seats = [Seat("S1", "a1", 0, 0), Seat("S2", "a2", 0, 0)],
        };
        var errors = await service.ReplaceAsync("tenant-1", "loc-1", map, "a", null, default);
        Assert.Empty(errors);
    }

    // ── Blocks ────────────────────────────────────────────────────────────────

    private static async Task<SeatMapService> ServiceWithSeededMap(InMemorySeatMapChangeRepository changeRepo)
    {
        var service = new SeatMapService(new InMemorySeatMapRepository(), new InMemorySeatBlockRepository(), changeRepo);
        var map = new SeatMap { Areas = [Area("a1", "team-a")], Seats = [Seat("S1", "a1", 0, 0), Seat("S2", "a1", 0, 1)] };
        await service.ReplaceAsync("tenant-1", "loc-1", map, "seed", null, default);
        return service;
    }

    [Fact]
    public async Task AddBlock_ValidSeatAndRange_PersistsAndRecordsChange()
    {
        var changeRepo = new InMemorySeatMapChangeRepository();
        var service = await ServiceWithSeededMap(changeRepo);

        var (blockId, errors) = await service.AddBlockAsync(
            "tenant-1", "loc-1", "S1",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5),
            SeatBlockReason.Maintenance, "desk repair", "hr-1", default);

        Assert.Empty(errors);
        Assert.NotNull(blockId);
        var blocks = await service.GetBlocksAsync("tenant-1", "loc-1", default);
        var block = Assert.Single(blocks);
        Assert.Equal("S1", block.SeatId);
        Assert.Equal(SeatBlockReason.Maintenance, block.Reason);

        var history = await changeRepo.GetHistoryAsync("tenant-1", "loc-1", 10, default);
        Assert.Contains(history, c =>
            c.ChangeType == SeatMapChangeRecord.TypeSeatBlocked &&
            c.SeatId == "S1" &&
            c.BlockedFrom == new DateOnly(2026, 8, 1) &&
            c.BlockedTo == new DateOnly(2026, 8, 5) &&
            c.ChangedByUserId == "hr-1" &&
            c.BlockReason == SeatBlockReason.Maintenance);
    }

    [Fact]
    public async Task AddBlock_UnknownSeat_ReturnsError()
    {
        var service = await ServiceWithSeededMap(new InMemorySeatMapChangeRepository());
        var (blockId, errors) = await service.AddBlockAsync(
            "tenant-1", "loc-1", "no-such-seat",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1),
            SeatBlockReason.Other, null, "hr-1", default);

        Assert.Null(blockId);
        Assert.Contains(errors, e => e.Contains("no-such-seat"));
    }

    [Fact]
    public async Task AddBlock_ToDateBeforeFromDate_ReturnsError()
    {
        var service = await ServiceWithSeededMap(new InMemorySeatMapChangeRepository());
        var (_, errors) = await service.AddBlockAsync(
            "tenant-1", "loc-1", "S1",
            new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1),
            SeatBlockReason.Other, null, "hr-1", default);

        Assert.Contains(errors, e => e.Contains("toDate"));
    }

    [Fact]
    public async Task RemoveBlock_ExistingBlock_RemovesAndRecordsChange()
    {
        var changeRepo = new InMemorySeatMapChangeRepository();
        var service = await ServiceWithSeededMap(changeRepo);
        var (blockId, _) = await service.AddBlockAsync(
            "tenant-1", "loc-1", "S1",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5),
            SeatBlockReason.Reserved, null, "hr-1", default);

        var removed = await service.RemoveBlockAsync("tenant-1", "loc-1", blockId!, "hr-2", "no longer needed", default);

        Assert.True(removed);
        Assert.Empty(await service.GetBlocksAsync("tenant-1", "loc-1", default));
        var history = await changeRepo.GetHistoryAsync("tenant-1", "loc-1", 10, default);
        Assert.Contains(history, c =>
            c.ChangeType == SeatMapChangeRecord.TypeSeatUnblocked &&
            c.SeatId == "S1" &&
            c.ChangedByUserId == "hr-2" &&
            c.ChangeReason == "no longer needed");
    }

    [Fact]
    public async Task RemoveBlock_UnknownBlock_ReturnsFalse()
    {
        var service = await ServiceWithSeededMap(new InMemorySeatMapChangeRepository());
        Assert.False(await service.RemoveBlockAsync("tenant-1", "loc-1", "missing", "hr-1", null, default));
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Maps_AreTenantScoped()
    {
        var service = MakeService(out _);
        var map = new SeatMap { Areas = [Area("a1")], Seats = [Seat("S1", "a1")] };
        await service.ReplaceAsync("tenant-1", "loc-1", map, "a", null, default);

        var other = await service.GetMapAsync("tenant-2", "loc-1", default);
        Assert.Empty(other.Areas);
        Assert.Empty(other.Seats);
    }
}
