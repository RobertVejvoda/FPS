namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetHrSlotHistoryHandlerTests
{
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly GetHrSlotHistoryHandler handler;

    private static readonly HrSlotHistoryResult EmptyResult = new("M1-1", [], null);

    public GetHrSlotHistoryHandlerTests()
    {
        handler = new GetHrSlotHistoryHandler(queryRepository.Object);

        queryRepository
            .Setup(r => r.GetSlotHistoryAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult);
    }

    [Fact]
    public async Task Handle_PassesTenantSlotAndLocationToRepository()
    {
        string? capturedTenant = null;
        string? capturedLocation = null;
        string? capturedSlot = null;
        queryRepository
            .Setup(r => r.GetSlotHistoryAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string, DateOnly?, DateOnly?, int, string?, CancellationToken>(
                (tenantId, locationId, slotId, _, _, _, _, _) =>
                { capturedTenant = tenantId; capturedLocation = locationId; capturedSlot = slotId; })
            .ReturnsAsync(EmptyResult);

        await handler.Handle(QueryWith(tenantId: "tenant-hr", locationId: "Prague", slotId: "M1-1"), CancellationToken.None);

        Assert.Equal("tenant-hr", capturedTenant);
        Assert.Equal("Prague", capturedLocation);
        Assert.Equal("M1-1", capturedSlot);
    }

    [Fact]
    public async Task Handle_PassesDateWindowAndPagingToRepository()
    {
        DateOnly? capturedFrom = null;
        DateOnly? capturedTo = null;
        int? capturedPageSize = null;
        string? capturedCursor = null;
        queryRepository
            .Setup(r => r.GetSlotHistoryAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string, DateOnly?, DateOnly?, int, string?, CancellationToken>(
                (_, _, _, from, to, pageSize, cursor, _) =>
                { capturedFrom = from; capturedTo = to; capturedPageSize = pageSize; capturedCursor = cursor; })
            .ReturnsAsync(EmptyResult);

        var from = new DateOnly(2026, 5, 1);
        var to = new DateOnly(2026, 6, 1);
        await handler.Handle(
            QueryWith(from: from, to: to, pageSize: 25, cursor: "abc"),
            CancellationToken.None);

        Assert.Equal(from, capturedFrom);
        Assert.Equal(to, capturedTo);
        Assert.Equal(25, capturedPageSize);
        Assert.Equal("abc", capturedCursor);
    }

    [Fact]
    public async Task Handle_ReturnsRepositoryResultDirectly()
    {
        var items = new List<HrSlotHistoryItem>
        {
            new(Guid.NewGuid(), "ref-a",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                new TimeOnly(8, 0), new TimeOnly(18, 0), "Prague",
                "Allocated", null, null, "M1-1",
                DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-1))
        };
        var expected = new HrSlotHistoryResult("M1-1", items, "next-cursor", 1);

        queryRepository
            .Setup(r => r.GetSlotHistoryAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await handler.Handle(QueryWith(), CancellationToken.None);

        Assert.Equal("M1-1", result.SlotId);
        Assert.Single(result.Items);
        Assert.Equal("next-cursor", result.NextCursor);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_EmptyResult_HasNoCursorOrItems()
    {
        var result = await handler.Handle(QueryWith(), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    private static GetHrSlotHistoryQuery QueryWith(
        string tenantId = "tenant-1",
        string? locationId = null,
        string slotId = "M1-1",
        DateOnly? from = null,
        DateOnly? to = null,
        int pageSize = 50,
        string? cursor = null)
        => new(tenantId, locationId, slotId, from, to, pageSize, cursor);
}
