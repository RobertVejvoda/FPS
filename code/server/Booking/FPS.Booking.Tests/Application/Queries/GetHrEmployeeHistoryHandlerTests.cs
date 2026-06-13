namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetHrEmployeeHistoryHandlerTests
{
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly GetHrEmployeeHistoryHandler handler;

    private static readonly HrEmployeeHistoryResult EmptyResult =
        new("user-1", new HrEmployeeHistorySummary(0, 0, 0, 0, 0), [], null);

    public GetHrEmployeeHistoryHandlerTests()
    {
        handler = new GetHrEmployeeHistoryHandler(queryRepository.Object);

        queryRepository
            .Setup(r => r.GetEmployeeHistoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult);
    }

    [Fact]
    public async Task Handle_PassesTenantAndRequestorIdToRepository()
    {
        string? capturedTenant = null;
        string? capturedRequestor = null;
        queryRepository
            .Setup(r => r.GetEmployeeHistoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly?, DateOnly?, string?, int, string?, CancellationToken>(
                (tenantId, requestorId, _, _, _, _, _, _) => { capturedTenant = tenantId; capturedRequestor = requestorId; })
            .ReturnsAsync(EmptyResult);

        await handler.Handle(QueryWith(tenantId: "tenant-hr", requestorId: "user-99"), CancellationToken.None);

        Assert.Equal("tenant-hr", capturedTenant);
        Assert.Equal("user-99", capturedRequestor);
    }

    [Fact]
    public async Task Handle_PassesFiltersToRepository()
    {
        DateOnly? capturedFrom = null;
        DateOnly? capturedTo = null;
        string? capturedStatus = null;
        int? capturedPageSize = null;

        queryRepository
            .Setup(r => r.GetEmployeeHistoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly?, DateOnly?, string?, int, string?, CancellationToken>(
                (_, _, from, to, status, pageSize, _, _) =>
                { capturedFrom = from; capturedTo = to; capturedStatus = status; capturedPageSize = pageSize; })
            .ReturnsAsync(EmptyResult);

        var from = new DateOnly(2026, 5, 1);
        var to = new DateOnly(2026, 6, 1);

        await handler.Handle(
            QueryWith(from: from, to: to, statusFilter: "Allocated", pageSize: 25),
            CancellationToken.None);

        Assert.Equal(from, capturedFrom);
        Assert.Equal(to, capturedTo);
        Assert.Equal("Allocated", capturedStatus);
        Assert.Equal(25, capturedPageSize);
    }

    [Fact]
    public async Task Handle_ReturnsRepositoryResultDirectly()
    {
        var items = new List<HrEmployeeHistoryItem>
        {
            new(Guid.NewGuid(),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
                new TimeOnly(8, 0), new TimeOnly(18, 0), "Prague",
                "Allocated", null, null, "slot-1",
                DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(-3)),
            new(Guid.NewGuid(),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                new TimeOnly(8, 0), new TimeOnly(18, 0), "Prague",
                "Rejected", "DailyCapExceeded", "Daily cap exceeded", null,
                DateTime.UtcNow.AddDays(-12), DateTime.UtcNow.AddDays(-10)),
        };
        var summary = new HrEmployeeHistorySummary(Total: 5, Allocated: 2, Rejected: 1, Cancelled: 1, Pending: 1);
        var expected = new HrEmployeeHistoryResult("user-1", summary, items, "next-cursor", 5);

        queryRepository
            .Setup(r => r.GetEmployeeHistoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await handler.Handle(QueryWith(), CancellationToken.None);

        Assert.Equal(2, result.Summary.Allocated);
        Assert.Equal(1, result.Summary.Rejected);
        Assert.Equal(1, result.Summary.Cancelled);
        Assert.Equal("next-cursor", result.NextCursor);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsZeroSummary()
    {
        var result = await handler.Handle(QueryWith(), CancellationToken.None);

        Assert.Equal(0, result.Summary.Total);
        Assert.Equal(0, result.Summary.Allocated);
        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    private static GetHrEmployeeHistoryQuery QueryWith(
        string tenantId = "tenant-1",
        string requestorId = "user-1",
        DateOnly? from = null,
        DateOnly? to = null,
        string? statusFilter = null,
        int pageSize = 50,
        string? cursor = null)
        => new(tenantId, requestorId, from, to, statusFilter, pageSize, cursor);
}
