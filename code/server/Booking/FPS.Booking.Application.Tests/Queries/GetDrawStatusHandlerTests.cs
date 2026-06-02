using FPS.Booking.Application.Queries;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetDrawStatusHandlerTests
{
    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly Mock<IAvailableSlotService> slotService = new();
    private readonly Mock<ITenantPolicyService> policyService = new();
    private readonly GetDrawStatusHandler handler;

    private static readonly DateOnly DrawDate = new(2026, 6, 2);
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<AvailableSlot> DefaultSlots =
        Enumerable.Range(0, 10).Select(_ => AvailableSlot.Create(ParkingSlotId.FromString($"S{_}"))).ToList();

    private static readonly TenantPolicy DefaultPolicy = new(
        DailyRequestCap: 100,
        DrawCutOffTime: new TimeOnly(18, 0),
        TimeZoneId: "UTC",
        SameDayBookingEnabled: false);

    public GetDrawStatusHandlerTests()
    {
        slotService
            .Setup(s => s.GetAvailableSlotsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultSlots);

        policyService
            .Setup(p => p.GetEffectivePolicyAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy);

        handler = new GetDrawStatusHandler(drawRepository.Object, slotService.Object, policyService.Object);
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
    public async Task Handle_NoDraw_ReturnsPreDrawDefault()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("NotScheduled", result.Status);
        Assert.Equal("Unknown", result.DemandLevel);
        Assert.Equal(0, result.RequestCount);
        Assert.True(result.CanRequest);
        Assert.Null(result.CannotRequestReason);
    }

    [Fact]
    public async Task Handle_NoDraw_PreservesQueryContext()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("loc-1", result!.LocationId);
        Assert.Equal(DrawDate, result.Date);
        Assert.Equal("tenant-1", result.TenantId);
    }

    [Fact]
    public async Task Handle_CompletedDraw_CanRequestIsFalse()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt(allocated: 3, rejected: 2, waitlisted: 1));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.False(result!.CanRequest);
        Assert.NotNull(result.CannotRequestReason);
    }

    [Fact]
    public async Task Handle_PastDate_CanRequestIsFalse()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        var pastQuery = new GetDrawStatusQuery(
            TenantId: "tenant-1",
            LocationId: "loc-1",
            Date: new DateOnly(2025, 1, 1),
            TimeSlotStart: new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            TimeSlotEnd: new DateTime(2025, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var result = await handler.Handle(pastQuery, CancellationToken.None);

        Assert.False(result.CanRequest);
        Assert.Equal("Date has passed", result.CannotRequestReason);
    }

    [Fact]
    public async Task Handle_InProgressDraw_CanRequestIsFalse()
    {
        var attempt = CompletedAttempt(0, 0, 0);
        attempt.Status = "InProgress";
        attempt.CompletedAt = null;
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.False(result.CanRequest);
        Assert.NotNull(result.CannotRequestReason);
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

    [Fact]
    public async Task Handle_AllAllocated_DemandLevelIsLow()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt(allocated: 10, rejected: 0, waitlisted: 0));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("Low", result!.DemandLevel);
    }

    [Fact]
    public async Task Handle_MajorityAllocated_DemandLevelIsMedium()
    {
        // 7/10 = 70% satisfaction → Medium
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt(allocated: 7, rejected: 2, waitlisted: 1));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("Medium", result!.DemandLevel);
    }

    [Fact]
    public async Task Handle_MajorityUnfulfilled_DemandLevelIsHigh()
    {
        // 3/10 = 30% satisfaction → High
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt(allocated: 3, rejected: 5, waitlisted: 2));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("High", result!.DemandLevel);
    }

    [Fact]
    public async Task Handle_PendingDraw_DemandLevelIsUnknown()
    {
        var attempt = CompletedAttempt(0, 0, 0);
        attempt.Status = "Pending";
        attempt.CompletedAt = null;
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("Unknown", result!.DemandLevel);
    }

        [Fact]
    public async Task Handle_NoDraw_AvailableSpotCountFromSlotService()
    {
        var slots = Enumerable.Range(0, 5).Select(i => AvailableSlot.Create(ParkingSlotId.FromString($"A{i}"))).ToList();
        slotService
            .Setup(s => s.GetAvailableSlotsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(5, result.AvailableSpotCount);
    }

    [Fact]
    public async Task Handle_CompletedDraw_AvailableSpotCountFromSlotService()
    {
        var slots = Enumerable.Range(0, 24).Select(i => AvailableSlot.Create(ParkingSlotId.FromString($"B{i}"))).ToList();
        slotService
            .Setup(s => s.GetAvailableSlotsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt(allocated: 20, rejected: 4, waitlisted: 0));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal(24, result.AvailableSpotCount);
    }

    [Fact]
    public async Task Handle_CallsSlotServiceWithQueryParameters()
    {
        string? capturedTenant = null;
        string? capturedLocation = null;
        DateOnly capturedDate = default;

        slotService
            .Setup(s => s.GetAvailableSlotsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<TimeSlot>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly, TimeSlot, CancellationToken>(
                (t, l, d, _, _) => { capturedTenant = t; capturedLocation = l; capturedDate = d; })
            .ReturnsAsync(DefaultSlots);
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);

        await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("tenant-1", capturedTenant);
        Assert.Equal("loc-1", capturedLocation);
        Assert.Equal(DrawDate, capturedDate);
    }

    // ── Schedule metadata (DRAW005) ──────────────────────────────────────────

    [Fact]
    public async Task Handle_NullPolicy_ReturnsNotConfiguredScheduleStatus()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);
        policyService
            .Setup(p => p.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPolicy)null!);

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("notConfigured", result.ScheduleStatus);
        Assert.Null(result.CutOffAt);
        Assert.Null(result.NextDrawAt);
        Assert.Equal("UTC", result.TimeZone);
        Assert.NotEmpty(result.SafeMessage);
    }

    [Fact]
    public async Task Handle_KnownPolicy_ReturnsCutOffAtAndTimeZone()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);
        policyService
            .Setup(p => p.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPolicy(DailyRequestCap: 50, DrawCutOffTime: new TimeOnly(18, 0), TimeZoneId: "UTC", SameDayBookingEnabled: false));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("known", result.ScheduleStatus);
        Assert.Equal("UTC", result.TimeZone);
        Assert.NotNull(result.CutOffAt);
        Assert.Contains("18:00", result.CutOffAt);
        Assert.Equal("tenantPolicy", result.ScheduleSource);
    }

    [Fact]
    public async Task Handle_KnownPolicy_CutOffAtIsOnDayBeforeParkingDate()
    {
        // Parking date is DrawDate (2026-06-02); cut-off must fall on 2026-06-01
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);
        policyService
            .Setup(p => p.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPolicy(DailyRequestCap: 50, DrawCutOffTime: new TimeOnly(18, 0), TimeZoneId: "UTC", SameDayBookingEnabled: false));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result.CutOffAt);
        var cutOff = DateTimeOffset.Parse(result.CutOffAt);
        var expectedDay = DrawDate.AddDays(-1);
        Assert.Equal(expectedDay.Year, cutOff.Year);
        Assert.Equal(expectedDay.Month, cutOff.Month);
        Assert.Equal(expectedDay.Day, cutOff.Day);
        Assert.Equal(18, cutOff.Hour);
    }

    [Fact]
    public async Task Handle_KnownPolicy_NextDrawAtEqualsCutOffAt()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);
        policyService
            .Setup(p => p.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPolicy(DailyRequestCap: 50, DrawCutOffTime: new TimeOnly(18, 0), TimeZoneId: "UTC", SameDayBookingEnabled: false));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.NotNull(result.NextDrawAt);
        Assert.Equal(result.CutOffAt, result.NextDrawAt);
    }

    [Fact]
    public async Task Handle_CompletedDraw_RequestWindowStatusIsClosed()
    {
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedAttempt(allocated: 3, rejected: 2, waitlisted: 0));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("closed", result.RequestWindowStatus);
        Assert.Contains("complete", result.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_NoDraw_RequestWindowOpen_SafeMessageDescribesCutOff()
    {
        // DrawDate is in the past relative to test execution; CanRequest=false but window message shows cut-off
        drawRepository.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DrawAttemptDto?)null);
        policyService
            .Setup(p => p.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPolicy(DailyRequestCap: 50, DrawCutOffTime: new TimeOnly(18, 0), TimeZoneId: "Europe/Prague", SameDayBookingEnabled: false));

        var result = await handler.Handle(ValidQuery(), CancellationToken.None);

        Assert.Equal("known", result.ScheduleStatus);
        Assert.Equal("Europe/Prague", result.TimeZone);
        Assert.NotNull(result.SafeMessage);
        Assert.NotEmpty(result.SafeMessage);
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
