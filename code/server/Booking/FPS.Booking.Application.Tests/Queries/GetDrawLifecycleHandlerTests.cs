using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetDrawLifecycleHandlerTests
{
    private readonly Mock<IDrawRepository> drawRepo = new();
    private readonly GetDrawLifecycleHandler handler;

    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    public GetDrawLifecycleHandlerTests()
    {
        handler = new GetDrawLifecycleHandler(drawRepo.Object);
    }

    [Fact]
    public async Task Handle_DrawDoesNotExist_ReturnsNull()
    {
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_DrawExists_ReturnsLifecycleWithSteps()
    {
        var attempt = CompletedDrawAttempt();
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(attempt.DrawKey, result.DrawKey);
        Assert.Equal(attempt.Status, result.Status);
        Assert.Equal(attempt.Steps.Count, result.Steps.Count);
    }

    [Fact]
    public async Task Handle_DrawExists_ReturnsDecisions()
    {
        var attempt = CompletedDrawAttempt();
        attempt.Decisions =
        [
            new DrawDecisionDto
            {
                RequestId = Guid.NewGuid().ToString(),
                RequestorId = Guid.NewGuid().ToString(),
                Outcome = "Allocated",
                SlotId = "S1",
                Reason = null
            },
            new DrawDecisionDto
            {
                RequestId = Guid.NewGuid().ToString(),
                RequestorId = Guid.NewGuid().ToString(),
                Outcome = "Rejected",
                SlotId = null,
                Reason = "Not selected in draw"
            }
        ];

        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Decisions.Count);
        Assert.Contains(result.Decisions, d => d.Outcome == "Allocated");
        Assert.Contains(result.Decisions, d => d.Outcome == "Rejected");
    }

    [Fact]
    public async Task Handle_DrawExists_ReturnsDeterministicEvidence()
    {
        var attempt = CompletedDrawAttempt();
        attempt.Seed = 123456789L;
        attempt.AlgorithmVersion = "v1.2";
        attempt.Tier2CandidateSequence = ["req-1", "req-2", "req-3"];

        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(123456789L, result.Seed);
        Assert.Equal("v1.2", result.AlgorithmVersion);
        Assert.Equal(3, result.Tier2CandidateSequence.Count);
    }

    [Fact]
    public async Task Handle_DrawExists_ReturnsCorrelationMetadata()
    {
        var attempt = CompletedDrawAttempt();
        attempt.CorrelationId = "corr-123";
        attempt.TraceId = "trace-456";

        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("corr-123", result.CorrelationId);
        Assert.Equal("trace-456", result.TraceId);
    }

    private static GetDrawLifecycleQuery ValidQuery() => new(
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd);

    private static DrawAttemptDto CompletedDrawAttempt() => new()
    {
        DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900",
        TenantId = "tenant-1",
        LocationId = "loc-1",
        Date = DrawDate,
        Status = "Completed",
        Seed = 12345,
        AlgorithmVersion = "v1.0",
        AllocatedCount = 2,
        RejectedCount = 1,
        WaitlistedCount = 0,
        StartedAt = DateTime.UtcNow.AddMinutes(-5),
        CompletedAt = DateTime.UtcNow,
        Decisions = [],
        Tier2CandidateSequence = [],
        Steps =
        [
            new DrawLifecycleStepDto
            {
                StepName = "PolicyResolved",
                Status = "Completed",
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow.AddMinutes(-4),
                Summary = "Retrieved and validated tenant allocation policy",
                CorrelationId = "corr-123"
            },
            new DrawLifecycleStepDto
            {
                StepName = "RequestsLoaded",
                Status = "Completed",
                StartedAt = DateTime.UtcNow.AddMinutes(-4),
                CompletedAt = DateTime.UtcNow.AddMinutes(-3),
                Summary = "Loaded 3 pending booking request(s)",
                CorrelationId = "corr-123"
            }
        ],
        CorrelationId = "corr-123",
        TraceId = "trace-456"
    };
}
