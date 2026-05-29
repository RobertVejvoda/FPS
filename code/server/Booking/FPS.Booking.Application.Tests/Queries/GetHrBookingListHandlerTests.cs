namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetHrBookingListHandlerTests
{
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly GetHrBookingListHandler handler;

    private static readonly HrBookingListResult EmptyResult = new([], null);

    public GetHrBookingListHandlerTests()
    {
        handler = new GetHrBookingListHandler(queryRepository.Object);

        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult);
    }

    [Fact]
    public async Task Handle_PassesTenantIdToRepository()
    {
        string? capturedTenant = null;
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateOnly?, DateOnly?, string?, int, string?, CancellationToken>(
                (tenantId, _, _, _, _, _, _) => capturedTenant = tenantId)
            .ReturnsAsync(EmptyResult);

        await handler.Handle(QueryWith(tenantId: "tenant-hr"), CancellationToken.None);

        Assert.Equal("tenant-hr", capturedTenant);
    }

    [Fact]
    public async Task Handle_PassesFiltersToRepository()
    {
        DateOnly? capturedFrom = null;
        DateOnly? capturedTo = null;
        string? capturedStatus = null;

        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateOnly?, DateOnly?, string?, int, string?, CancellationToken>(
                (_, from, to, status, _, _, _) => { capturedFrom = from; capturedTo = to; capturedStatus = status; })
            .ReturnsAsync(EmptyResult);

        var from = new DateOnly(2026, 6, 1);
        var to = new DateOnly(2026, 6, 30);

        await handler.Handle(QueryWith(from: from, to: to, statusFilter: "Pending"), CancellationToken.None);

        Assert.Equal(from, capturedFrom);
        Assert.Equal(to, capturedTo);
        Assert.Equal("Pending", capturedStatus);
    }

    [Fact]
    public async Task Handle_ReturnsRepositoryResultDirectly()
    {
        var items = new List<HrBookingListItem>
        {
            new(Guid.NewGuid(), "user-ref", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                new TimeOnly(8, 0), new TimeOnly(18, 0), "Prague",
                "Pending", null, null, null,
                DateTime.UtcNow, DateTime.UtcNow)
        };
        var expected = new HrBookingListResult(items, "next-cursor", 1);

        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await handler.Handle(QueryWith(), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("next-cursor", result.NextCursor);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_EmptyTenant_ReturnsEmptyResult()
    {
        var result = await handler.Handle(QueryWith(), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task Handle_PageSizePassedToRepository()
    {
        int? capturedPageSize = null;
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateOnly?, DateOnly?, string?, int, string?, CancellationToken>(
                (_, _, _, _, pageSize, _, _) => capturedPageSize = pageSize)
            .ReturnsAsync(EmptyResult);

        await handler.Handle(QueryWith(pageSize: 25), CancellationToken.None);

        Assert.Equal(25, capturedPageSize);
    }

    private static GetHrBookingListQuery QueryWith(
        string tenantId = "tenant-1",
        DateOnly? from = null,
        DateOnly? to = null,
        string? statusFilter = null,
        int pageSize = 50,
        string? cursor = null)
        => new(tenantId, from, to, statusFilter, pageSize, cursor);
}
