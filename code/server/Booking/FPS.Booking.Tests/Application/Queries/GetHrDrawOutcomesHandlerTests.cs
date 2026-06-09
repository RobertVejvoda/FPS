namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetHrDrawOutcomesHandlerTests
{
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly GetHrDrawOutcomesHandler handler;

    private static readonly DateOnly DrawDate = new(2026, 6, 1);
    private static readonly TimeOnly SlotStart = new(9, 0);
    private static readonly TimeOnly SlotEnd = new(17, 0);

    private static readonly HrBookingListResult EmptyResult = new([], null);

    public GetHrDrawOutcomesHandlerTests()
    {
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult);

        drawRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        handler = new GetHrDrawOutcomesHandler(queryRepository.Object, drawRepository.Object);
    }

    [Fact]
    public async Task Handle_NoBookings_ReturnsEmptyList()
    {
        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_OnlyNonTerminalBookings_ReturnsEmptyList()
    {
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "EMP001", status: "Pending"),
                MakeItem(requestorRef: "EMP002", status: "InProgress"),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_AllocatedAndRejected_GroupedIntoOneDraw()
    {
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "EMP001", status: "Allocated"),
                MakeItem(requestorRef: "EMP002", status: "Rejected"),
                MakeItem(requestorRef: "EMP003", status: "Allocated"),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(3, result[0].TotalRequests);
    }

    [Fact]
    public async Task Handle_TwoDistinctDates_ReturnsTwoDraws()
    {
        var date1 = new DateOnly(2026, 6, 1);
        var date2 = new DateOnly(2026, 6, 2);

        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "A", status: "Allocated", date: date1),
                MakeItem(requestorRef: "B", status: "Rejected", date: date2),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Handle_TwoDistinctLocations_ReturnsTwoDraws()
    {
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "A", status: "Allocated", locationId: "Prague"),
                MakeItem(requestorRef: "B", status: "Rejected", locationId: "Brno"),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Handle_DrawnOutcomesHaveCorrectCounts_WithoutDrawAttempt()
    {
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "EMP001", status: "Allocated"),
                MakeItem(requestorRef: "EMP002", status: "Allocated"),
                MakeItem(requestorRef: "EMP003", status: "Rejected"),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(2, result[0].AllocatedCount);
        Assert.Equal(1, result[0].RejectedCount);
        Assert.Equal(3, result[0].TotalRequests);
    }

    [Fact]
    public async Task Handle_DrawAttemptFound_UsesOfficialCounts()
    {
        var attempt = new DrawAttemptDto
        {
            DrawKey = $"draw:tenant-1:loc-1:{DrawDate:yyyy-MM-dd}:{SlotStart:HHmm}",
            TenantId = "tenant-1",
            LocationId = "loc-1",
            Date = DrawDate,
            Status = "Completed",
            AllocatedCount = 5,
            RejectedCount = 3,
            WaitlistedCount = 1,
            CompletedAt = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc),
        };
        drawRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "EMP001", status: "Allocated"),
                MakeItem(requestorRef: "EMP002", status: "Rejected"),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(5, result[0].AllocatedCount);
        Assert.Equal(3, result[0].RejectedCount);
        Assert.Equal(1, result[0].WaitlistedCount);
        Assert.Equal("Completed", result[0].DrawStatus);
        Assert.NotNull(result[0].CompletedAt);
    }

    [Fact]
    public async Task Handle_DrawAttemptNotFound_FallsBackToCountedStatus()
    {
        drawRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "EMP001", status: "Allocated"),
                MakeItem(requestorRef: "EMP002", status: "Rejected"),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Completed", result[0].DrawStatus);
        Assert.Null(result[0].CompletedAt);
    }

    [Fact]
    public async Task Handle_OutcomeItemsMappedCorrectly()
    {
        var requestId = Guid.NewGuid();
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestId: requestId, requestorRef: "EMP999", status: "Allocated",
                    reasonCode: null, reason: null, allocatedSlotId: "SLOT-42"),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        var outcome = Assert.Single(result[0].Outcomes);
        Assert.Equal(requestId, outcome.RequestId);
        Assert.Equal("EMP999", outcome.RequestorRef);
        Assert.Equal("Allocated", outcome.Outcome);
        Assert.Equal("SLOT-42", outcome.AllocatedSlotId);
    }

    [Fact]
    public async Task Handle_DrawKeyConstructedFromBookingData()
    {
        string? capturedKey = null;
        drawRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedKey = key)
            .ReturnsAsync((DrawAttemptDto?)null);

        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "EMP001", status: "Allocated",
                    locationId: "loc-1", date: DrawDate, slotStart: SlotStart),
            ], null));

        await handler.Handle(ValidQuery(), CancellationToken.None);

        // draw:{tenantId}:{locationId}:{date:yyyy-MM-dd}:{slotStart:HHmm}
        Assert.Equal($"draw:tenant-1:loc-1:{DrawDate:yyyy-MM-dd}:{SlotStart:HHmm}", capturedKey);
    }

    [Fact]
    public async Task Handle_ResultsOrderedByDateDescending()
    {
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "A", status: "Allocated", date: new DateOnly(2026, 5, 1)),
                MakeItem(requestorRef: "B", status: "Rejected", date: new DateOnly(2026, 6, 1)),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("2026-06-01", result[0].Date);
        Assert.Equal("2026-05-01", result[1].Date);
    }

    [Fact]
    public async Task Handle_PassesTenantIdToRepository()
    {
        string? capturedTenant = null;
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, DateOnly?, DateOnly?, string?, int, string?, CancellationToken>(
                (tenantId, _, _, _, _, _, _, _) => capturedTenant = tenantId)
            .ReturnsAsync(EmptyResult);

        await handler.Handle(ValidQuery(tenantId: "my-tenant"), CancellationToken.None);

        Assert.Equal("my-tenant", capturedTenant);
    }

    [Fact]
    public async Task Handle_TimeSlotFormatted_AsHhMmRange()
    {
        queryRepository
            .Setup(r => r.GetByTenantAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrBookingListResult([
                MakeItem(requestorRef: "EMP001", status: "Allocated",
                    slotStart: new TimeOnly(9, 0), slotEnd: new TimeOnly(17, 0)),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("09:00-17:00", result[0].TimeSlot);
    }

    private static GetHrDrawOutcomesQuery ValidQuery(string tenantId = "tenant-1") =>
        new(tenantId,
            LocationId: null,
            From: new DateOnly(2026, 5, 1),
            To: new DateOnly(2026, 6, 30));

    private static HrBookingListItem MakeItem(
        Guid? requestId = null,
        string requestorRef = "EMP001",
        string status = "Allocated",
        string? reasonCode = null,
        string? reason = null,
        string? allocatedSlotId = null,
        string locationId = "loc-1",
        DateOnly? date = null,
        TimeOnly? slotStart = null,
        TimeOnly? slotEnd = null)
        => new(
            RequestId: requestId ?? Guid.NewGuid(),
            RequestorRef: requestorRef,
            RequestedDate: date ?? DrawDate,
            TimeSlotStart: slotStart ?? SlotStart,
            TimeSlotEnd: slotEnd ?? SlotEnd,
            LocationId: locationId,
            Status: status,
            ReasonCode: reasonCode,
            Reason: reason,
            AllocatedSlotId: allocatedSlotId,
            CreatedAt: DateTime.UtcNow,
            LastStatusChangedAt: DateTime.UtcNow);
}
