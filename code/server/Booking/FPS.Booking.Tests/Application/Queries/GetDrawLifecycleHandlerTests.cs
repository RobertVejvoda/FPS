using FPS.Booking.Application.Models;
using FPS.Booking.Application.Queries;
using FPS.Booking.Application.Repositories;
using Moq;

namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetDrawLifecycleHandlerTests
{
    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly GetDrawLifecycleHandler handler;

    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    public GetDrawLifecycleHandlerTests()
    {
        handler = new GetDrawLifecycleHandler(drawRepository.Object);
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
    public async Task Handle_CompletedDraw_AllStepsCompleted()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt());

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Completed", result!.Status);
        // All steps except EventsPublished should be Completed; EventsPublished is Attempted
        // (fire-and-forget delivery is not guaranteed, even on a completed draw)
        Assert.All(result.Steps.Where(s => s.Name != "EventsPublished"),
            s => Assert.Equal("Completed", s.Status));
        Assert.Equal("Attempted", result.Steps.Single(s => s.Name == "EventsPublished").Status);
    }

    [Fact]
    public async Task Handle_CompletedDraw_ContainsExpectedStepNames()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt());

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        var stepNames = result!.Steps.Select(s => s.Name).ToList();
        Assert.Contains("DrawStarted", stepNames);
        Assert.Contains("RequestsLoaded", stepNames);
        Assert.Contains("PolicyResolved", stepNames);
        Assert.Contains("WeightedAllocationCompleted", stepNames);
        Assert.Contains("DecisionsPersisted", stepNames);
        Assert.Contains("DrawCompleted", stepNames);
    }

    [Fact]
    public async Task Handle_PersistedSteps_UsedInsteadOfDerived()
    {
        var attempt = CompletedAttempt();
        attempt.LifecycleSteps =
        [
            new DrawLifecycleStepRecord { StepName = "PolicyResolved", Status = "Completed", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow },
            new DrawLifecycleStepRecord { StepName = "RequestsLoaded", Status = "Completed", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, Summary = "3 pending request(s)" },
            new DrawLifecycleStepRecord { StepName = "EventsPublished", Status = "Attempted", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow }
        ];
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Steps.Count);
        Assert.Equal("PolicyResolved", result.Steps[0].Name);
        Assert.Equal("3 pending request(s)", result.Steps[1].Summary);
        Assert.Equal("Attempted", result.Steps[2].Status);
    }

    [Fact]
    public async Task Handle_FailedDraw_LateStepsAreNotReached()
    {
        var attempt = CompletedAttempt();
        attempt.Status = "Failed";
        attempt.CompletedAt = null;
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("NotReached", result!.Steps.Single(s => s.Name == "DecisionsPersisted").Status);
        Assert.Equal("NotReached", result.Steps.Single(s => s.Name == "EventsPublished").Status);
        Assert.Equal("NotReached", result.Steps.Single(s => s.Name == "DrawCompleted").Status);
    }

    [Fact]
    public async Task Handle_DecisionsFormattedAsBusinessReferences()
    {
        var attempt = CompletedAttempt();
        attempt.Decisions =
        [
            new DrawDecisionDto { RequestId = "00000000-0000-0000-0000-0000000OABCD", Outcome = "Allocated", SlotId = null, Reason = null },
            new DrawDecisionDto { RequestId = "00000000-0000-0000-0000-00000000EF12", Outcome = "Rejected", SlotId = null, Reason = "No slots available." },
        ];
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.All(result!.Decisions, d => Assert.StartsWith("BK-20260602-", d.BookingReference));
        Assert.DoesNotContain(result.Decisions, d => d.BookingReference.Contains("00000000"));
    }

    [Fact]
    public async Task Handle_SlotIdGuid_ShowsAssignedSpaceLabel()
    {
        var attempt = CompletedAttempt();
        attempt.Decisions =
        [
            new DrawDecisionDto { RequestId = "aaa", Outcome = "Allocated", SlotId = "12345678-1234-1234-1234-123456789abc" },
        ];
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("Assigned space", result!.Decisions[0].SlotReference);
    }

    [Fact]
    public async Task Handle_SlotIdFriendly_StripsPrefixAndShowsSpaceLabel()
    {
        var attempt = CompletedAttempt();
        attempt.Decisions =
        [
            new DrawDecisionDto { RequestId = "bbb", Outcome = "Allocated", SlotId = "LOC-MAIN-42" },
        ];
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("Space 42", result!.Decisions[0].SlotReference);
    }

    [Fact]
    public async Task Handle_Tier2CandidateSequence_FormattedAsBusinessReferences()
    {
        var attempt = CompletedAttempt();
        attempt.Tier2CandidateSequence = ["req-aaaa-1111", "req-bbbb-2222"];
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(2, result!.Tier2CandidateSequence.Count);
        Assert.All(result.Tier2CandidateSequence, r => Assert.StartsWith("BK-20260602-", r));
    }

    [Fact]
    public async Task Handle_SeedAndAlgorithmVersion_IncludedInResult()
    {
        var attempt = CompletedAttempt();
        attempt.Seed = 99999;
        attempt.AlgorithmVersion = "2.1";
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(99999, result!.Seed);
        Assert.Equal("2.1", result.AlgorithmVersion);
        Assert.Contains("2.1", result.Steps.Single(s => s.Name == "PolicyResolved").Summary);
    }

    [Fact]
    public async Task Handle_RequestsLoadedStep_IncludesCount()
    {
        var attempt = CompletedAttempt();
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        var step = result!.Steps.Single(s => s.Name == "RequestsLoaded");
        Assert.Contains("3", step.Summary); // 3 decisions in CompletedAttempt
    }

    [Fact]
    public async Task Handle_WeightedAllocationStep_IncludesCounts()
    {
        var attempt = CompletedAttempt();
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        var step = result!.Steps.Single(s => s.Name == "WeightedAllocationCompleted");
        Assert.NotNull(step.Summary);
        Assert.Contains("allocated", step.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rejected", step.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_AuditReference_EqualsDrawKey()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt());

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(result!.DrawKey, result.AuditReference);
    }

    private static GetDrawLifecycleQuery ValidQuery() => new(
        TenantId: "tenant-1",
        LocationId: "loc-1",
        Date: DrawDate,
        TimeSlotStart: SlotStart,
        TimeSlotEnd: SlotEnd);

    private static DrawAttemptDto CompletedAttempt() => new()
    {
        DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900",
        TenantId = "tenant-1",
        LocationId = "loc-1",
        Date = DrawDate,
        Status = "Completed",
        Seed = 42,
        AlgorithmVersion = "1.0",
        AllocatedCount = 2,
        RejectedCount = 1,
        WaitlistedCount = 0,
        StartedAt = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc),
        CompletedAt = new DateTime(2026, 6, 1, 18, 0, 5, DateTimeKind.Utc),
        Decisions =
        [
            new DrawDecisionDto { RequestId = "req-0001", Outcome = "Allocated", SlotId = "LOC-MAIN-1" },
            new DrawDecisionDto { RequestId = "req-0002", Outcome = "Allocated", SlotId = "LOC-MAIN-2" },
            new DrawDecisionDto { RequestId = "req-0003", Outcome = "Rejected", Reason = "No available slots." },
        ],
        Tier2CandidateSequence = [],
    };
}

// Additional coverage: persisted steps on a failed draw
public sealed class GetDrawLifecycleHandlerFailedPersistedTests
{
    private const string SafeFailureMessage = "Draw workflow execution failed.";
    // Represents internal exception detail that must never be stored or returned to callers.
    private const string RawDiagnostic = "NullReferenceException at FPS.Booking.Allocator.Run()";

    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly GetDrawLifecycleHandler handler;

    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    public GetDrawLifecycleHandlerFailedPersistedTests()
    {
        handler = new GetDrawLifecycleHandler(drawRepository.Object);
    }

    [Fact]
    public async Task Handle_FailedDraw_WithPersistedSteps_ReturnsPersistedEvidenceNotDerived()
    {
        // DrawFailed step stores the generic safe message — never raw exception text.
        var attempt = new DrawAttemptDto
        {
            DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900",
            TenantId = "tenant-1",
            LocationId = "loc-1",
            Date = DrawDate,
            Status = "Failed",
            Seed = 99,
            AlgorithmVersion = string.Empty,
            StartedAt = new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 6, 2, 8, 0, 1, DateTimeKind.Utc),
            Decisions = [],
            LifecycleSteps =
            [
                new DrawLifecycleStepRecord { StepName = "PolicyResolved", Status = "Completed", StartedAt = DateTime.UtcNow },
                new DrawLifecycleStepRecord { StepName = "RequestsLoaded", Status = "Completed", StartedAt = DateTime.UtcNow },
                new DrawLifecycleStepRecord { StepName = "DrawFailed", Status = "Failed", StartedAt = DateTime.UtcNow, ErrorMessage = SafeFailureMessage },
            ]
        };
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(
            new GetDrawLifecycleQuery("tenant-1", "loc-1", DrawDate, SlotStart, SlotEnd),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Failed", result!.Status);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("DrawFailed", result.Steps[2].Name);
        Assert.Equal("Failed", result.Steps[2].Status);
        Assert.Equal(SafeFailureMessage, result.Steps[2].ErrorMessage);
    }

    /// <summary>
    /// Regression test: GET /draws/{date}/lifecycle must never expose raw exception
    /// or diagnostic text to hr_manager/admin/auditor callers.
    /// FailDrawAttemptActivity is responsible for writing only SafeErrorMessage into
    /// DrawLifecycleStepRecord.ErrorMessage; this test proves the handler faithfully
    /// reflects only what was stored and that raw diagnostic text is not present.
    /// </summary>
    [Fact]
    public async Task Handle_FailedDraw_LifecycleResponse_DoesNotExposeRawDiagnosticText()
    {
        // Simulate state as written by FailDrawAttemptActivity: safe message only.
        var attempt = new DrawAttemptDto
        {
            DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900",
            TenantId = "tenant-1",
            LocationId = "loc-1",
            Date = DrawDate,
            Status = "Failed",
            Seed = 12,
            AlgorithmVersion = string.Empty,
            StartedAt = new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc),
            Decisions = [],
            LifecycleSteps =
            [
                new DrawLifecycleStepRecord { StepName = "DrawFailed", Status = "Failed", StartedAt = DateTime.UtcNow, ErrorMessage = SafeFailureMessage },
            ]
        };
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(
            new GetDrawLifecycleQuery("tenant-1", "loc-1", DrawDate, SlotStart, SlotEnd),
            CancellationToken.None);

        Assert.NotNull(result);
        var failStep = result!.Steps.Single(s => s.Name == "DrawFailed");
        // The safe message is returned.
        Assert.Equal(SafeFailureMessage, failStep.ErrorMessage);
        // Raw diagnostic text must not appear anywhere in the step's ErrorMessage.
        Assert.DoesNotContain(RawDiagnostic, failStep.ErrorMessage ?? string.Empty);
    }
}
