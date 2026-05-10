using FPS.Booking.Application.Queries;

namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetDrawStatusHandlerTests
{
    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly GetDrawStatusHandler handler;

    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    public GetDrawStatusHandlerTests()
    {
        handler = new GetDrawStatusHandler(drawRepository.Object);
    }

    [Fact]
    public async Task Handle_CompletedDraw_ReturnsMappedResult()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt(allocated: 3, rejected: 2, waitlisted: 1));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Completed", result!.Status);
        Assert.Equal(3, result.AllocatedCount);
        Assert.Equal(2, result.RejectedCount);
        Assert.Equal(1, result.WaitlistedCount);
        Assert.Equal(6, result.RequestCount);
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNull()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_IncludesSeedAndAlgorithmVersion()
    {
        var attempt = CompletedAttempt(3, 2, 1);
        attempt.Seed = 12345;
        attempt.AlgorithmVersion = "1.0";
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(12345, result!.Seed);
        Assert.Equal("1.0", result.AlgorithmVersion);
    }

    [Fact]
    public async Task Handle_DerivesCompanyCarOverflowFromDecisions()
    {
        var attempt = CompletedAttempt(0, 2, 0);
        attempt.Decisions =
        [
            new DrawDecisionDto { Outcome = "Rejected", Reason = "Company-car capacity is full for this time slot." },
            new DrawDecisionDto { Outcome = "Rejected", Reason = "No available slots." }
        ];
        attempt.RejectedCount = 2;
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(1, result!.CompanyCarOverflowCount);
    }

    [Fact]
    public async Task Handle_DeduplicatesSummaryRejectionReasons()
    {
        var attempt = CompletedAttempt(0, 3, 0);
        attempt.Decisions =
        [
            new DrawDecisionDto { Outcome = "Rejected", Reason = "No slots available." },
            new DrawDecisionDto { Outcome = "Rejected", Reason = "No slots available." },
            new DrawDecisionDto { Outcome = "Rejected", Reason = "Duplicate request." }
        ];
        attempt.RejectedCount = 3;
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(2, result!.SummaryRejectionReasons.Count);
    }

    [Fact]
    public async Task Handle_IncludesTimestamps()
    {
        var started = new DateTime(2026, 6, 2, 18, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2026, 6, 2, 18, 0, 5, DateTimeKind.Utc);
        var attempt = CompletedAttempt(3, 2, 1);
        attempt.StartedAt = started;
        attempt.CompletedAt = completed;
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(started, result!.StartedAt);
        Assert.Equal(completed, result.CompletedAt);
    }

    private static GetDrawStatusQuery ValidQuery() => new(
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd);

    private static DrawAttemptDto CompletedAttempt(int allocated, int rejected, int waitlisted) => new()
    {
        DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900",
        TenantId = "tenant-1",
        LocationId = "loc-1",
        Date = DrawDate,
        Status = "Completed",
        Seed = 42,
        AlgorithmVersion = "1.0",
        AllocatedCount = allocated,
        RejectedCount = rejected,
        WaitlistedCount = waitlisted,
        StartedAt = DateTime.UtcNow,
        CompletedAt = DateTime.UtcNow,
        Decisions = Enumerable.Range(0, allocated + rejected + waitlisted)
            .Select(i => new DrawDecisionDto
            {
                Outcome = i < allocated ? "Allocated" : i < allocated + rejected ? "Rejected" : "Waitlisted"
            })
            .ToList()
    };
}
