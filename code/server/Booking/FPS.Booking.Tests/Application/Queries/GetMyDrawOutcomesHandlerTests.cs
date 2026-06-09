namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetMyDrawOutcomesHandlerTests
{
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly GetMyDrawOutcomesHandler handler;

    private static readonly DateOnly DrawDate = new(2026, 6, 1);
    private static readonly TimeOnly SlotStart = new(9, 0);
    private static readonly TimeOnly SlotEnd = new(17, 0);

    private static readonly BookingListResult EmptyResult = new([], null);

    public GetMyDrawOutcomesHandlerTests()
    {
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult);

        drawRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        handler = new GetMyDrawOutcomesHandler(queryRepository.Object, drawRepository.Object);
    }

    [Fact]
    public async Task Handle_NoBookings_ReturnsEmptyList()
    {
        var result = await handler.Handle(ValidQuery(), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_PendingBookings_ExcludedFromResults()
    {
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([MakeItem(status: "Pending")], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_AllocatedBooking_IncludedWithCorrectOutcome()
    {
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([MakeItem(status: "Allocated", allocatedSlotId: "SLOT-7")], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Allocated", result[0].MyOutcome);
        Assert.Equal("SLOT-7", result[0].MyAllocatedSlotId);
    }

    [Fact]
    public async Task Handle_RejectedBooking_IncludedWithReason()
    {
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([MakeItem(status: "Rejected", reason: "No capacity available.")], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Rejected", result[0].MyOutcome);
        Assert.Equal("No capacity available.", result[0].MyReason);
    }

    [Fact]
    public async Task Handle_TwoDrawDates_ReturnsTwoEntries()
    {
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([
                MakeItem(status: "Allocated", date: new DateOnly(2026, 6, 1)),
                MakeItem(status: "Rejected", date: new DateOnly(2026, 5, 28)),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Handle_DrawAttemptFound_UsesOfficialCounts()
    {
        var attempt = new DrawAttemptDto
        {
            DrawKey = $"draw:tenant-1:loc-1:{DrawDate:yyyy-MM-dd}:{SlotStart:HHmm}",
            TenantId = "tenant-1",
            Status = "Completed",
            AllocatedCount = 8,
            RejectedCount = 4,
            WaitlistedCount = 0,
            CompletedAt = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc),
        };
        drawRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([MakeItem(status: "Allocated")], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(8, result[0].AllocatedCount);
        Assert.Equal(12, result[0].TotalRequests);
        Assert.Equal("Completed", result[0].DrawStatus);
        Assert.NotNull(result[0].CompletedAt);
    }

    [Fact]
    public async Task Handle_DrawAttemptNotFound_FallsBack()
    {
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([MakeItem(status: "Rejected")], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Completed", result[0].DrawStatus);
        Assert.Null(result[0].CompletedAt);
    }

    [Fact]
    public async Task Handle_DrawKeyConstructedCorrectly()
    {
        string? capturedKey = null;
        drawRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedKey = key)
            .ReturnsAsync((DrawAttemptDto?)null);

        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([
                MakeItem(status: "Allocated", locationId: "loc-1", date: DrawDate, slotStart: SlotStart),
            ], null));

        await handler.Handle(ValidQuery(tenantId: "tenant-1"), CancellationToken.None);

        Assert.Equal($"draw:tenant-1:loc-1:{DrawDate:yyyy-MM-dd}:{SlotStart:HHmm}", capturedKey);
    }

    [Fact]
    public async Task Handle_ResultsOrderedByDateDescending()
    {
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([
                MakeItem(status: "Rejected", date: new DateOnly(2026, 5, 1)),
                MakeItem(status: "Allocated", date: new DateOnly(2026, 6, 1)),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("2026-06-01", result[0].Date);
        Assert.Equal("2026-05-01", result[1].Date);
    }

    [Fact]
    public async Task Handle_TimeSlotFormattedAsRange()
    {
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingListResult([
                MakeItem(status: "Allocated", slotStart: new TimeOnly(9, 0), slotEnd: new TimeOnly(17, 0)),
            ], null));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("09:00-17:00", result[0].TimeSlot);
    }

    [Fact]
    public async Task Handle_PassesRequestorIdToRepository()
    {
        string? capturedRequestorId = null;
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(),
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly, DateOnly?, string?, int, string?, CancellationToken>(
                (_, requestorId, _, _, _, _, _, _) => capturedRequestorId = requestorId)
            .ReturnsAsync(EmptyResult);

        await handler.Handle(ValidQuery(requestorId: "user-xyz"), CancellationToken.None);

        Assert.Equal("user-xyz", capturedRequestorId);
    }

    private static GetMyDrawOutcomesQuery ValidQuery(
        string tenantId = "tenant-1",
        string requestorId = "user-1") =>
        new(tenantId, requestorId, new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 30));

    private static BookingListItem MakeItem(
        string status = "Allocated",
        string? reason = null,
        string? allocatedSlotId = null,
        string locationId = "loc-1",
        DateOnly? date = null,
        TimeOnly? slotStart = null,
        TimeOnly? slotEnd = null) =>
        new(
            RequestId: Guid.NewGuid(),
            RequestedDate: date ?? DrawDate,
            TimeSlotStart: slotStart ?? SlotStart,
            TimeSlotEnd: slotEnd ?? SlotEnd,
            LocationId: locationId,
            Status: status,
            ReasonCode: null,
            Reason: reason,
            AllocatedSlotId: allocatedSlotId,
            NextAction: "None",
            CreatedAt: DateTime.UtcNow,
            LastStatusChangedAt: DateTime.UtcNow);
}
