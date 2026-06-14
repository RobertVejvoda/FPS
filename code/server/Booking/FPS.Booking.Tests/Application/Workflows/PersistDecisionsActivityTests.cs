using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Application.Workflows.Activities;

namespace FPS.Booking.Application.Tests.Workflows;

public sealed class PersistDecisionsActivityTests
{
    private readonly Mock<IBookingRepository> bookingRepo = new();
    private readonly Mock<IEmployeeMetricsService> metricsService = new();
    private readonly Mock<IDrawRepository> drawRepo = new();
    private readonly PersistDecisionsActivity activity;

    private const string TenantId = "tenant-1";
    private const string DrawKey = "draw:tenant-1:loc-1:2026-06-02:0900-1700";
    private static readonly DateOnly DrawDate = new(2026, 6, 2);

    public PersistDecisionsActivityTests()
    {
        activity = new PersistDecisionsActivity(bookingRepo.Object, metricsService.Object, drawRepo.Object);

        var drawAttempt = new DrawAttemptDto { DrawKey = DrawKey, LifecycleSteps = [] };
        drawRepo.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(drawAttempt);
        drawRepo.Setup(r => r.SaveAsync(It.IsAny<DrawAttemptDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Already-decided request is skipped on retry ───────────────────────────

    [Fact]
    public async Task RunAsync_AlreadyAllocatedRequest_SkipsUpdateAndMetrics()
    {
        var requestId = Guid.NewGuid();
        bookingRepo.Setup(r => r.GetBookingRequestAsync(TenantId, requestId))
            .ReturnsAsync(new BookingRequestDto { Status = "Allocated" });

        await activity.RunAsync(null!, MakeInput(requestId, "Allocated"));

        bookingRepo.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        metricsService.Verify(m => m.IncrementRecentAllocationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_AlreadyRejectedRequest_SkipsUpdate()
    {
        var requestId = Guid.NewGuid();
        bookingRepo.Setup(r => r.GetBookingRequestAsync(TenantId, requestId))
            .ReturnsAsync(new BookingRequestDto { Status = "Rejected" });

        await activity.RunAsync(null!, MakeInput(requestId, "Rejected"));

        bookingRepo.Verify(r => r.UpdateBookingRequestStatusAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Pending request is updated on first run ───────────────────────────────

    [Fact]
    public async Task RunAsync_PendingAllocated_UpdatesStatusAndIncrementsMetrics()
    {
        var requestId = Guid.NewGuid();
        bookingRepo.Setup(r => r.GetBookingRequestAsync(TenantId, requestId))
            .ReturnsAsync(new BookingRequestDto { Status = "Pending" });

        await activity.RunAsync(null!, MakeInput(requestId, "Allocated"));

        bookingRepo.Verify(r => r.UpdateBookingRequestStatusAsync(
            TenantId, requestId, "Allocated",
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        metricsService.Verify(m => m.IncrementRecentAllocationAsync(
            TenantId, "requestor-1", DrawDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Retry does not double-apply after first success ───────────────────────

    [Fact]
    public async Task RunAsync_RetryAfterAllocation_DoesNotDoubleApplyOrDoubleIncrement()
    {
        var requestId = Guid.NewGuid();

        // First call: request is Pending → gets allocated. Second call (retry): already Allocated.
        bookingRepo.SetupSequence(r => r.GetBookingRequestAsync(TenantId, requestId))
            .ReturnsAsync(new BookingRequestDto { Status = "Pending" })
            .ReturnsAsync(new BookingRequestDto { Status = "Allocated" });

        var input = MakeInput(requestId, "Allocated");

        await activity.RunAsync(null!, input);  // first execution
        await activity.RunAsync(null!, input);  // retry

        bookingRepo.Verify(r => r.UpdateBookingRequestStatusAsync(
            TenantId, requestId, "Allocated",
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        metricsService.Verify(m => m.IncrementRecentAllocationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CAP-468: allocated slot id persists back to the booking ───────────────

    [Fact]
    public async Task RunAsync_PendingAllocated_PersistsAllocatedSlotIdToBookingRequest()
    {
        // Regression test for the Codex review finding on PR #469: the Draw's
        // decision.SlotId (e.g. "M1-2" for a motorcycle unit) must flow back to
        // the booking, so HR/employee/map projections and cancel/reallocate can
        // see which capacity unit was assigned. Discovered when motorcycle units
        // were added because their string ids surfaced the gap immediately;
        // ordinary slot ids had silently fallen through the cracks as well.
        var requestId = Guid.NewGuid();
        bookingRepo.Setup(r => r.GetBookingRequestAsync(TenantId, requestId))
            .ReturnsAsync(new BookingRequestDto { Status = "Pending" });

        var input = new PersistDecisionsInput(DrawKey, TenantId, "2026-06-02",
            [new DrawDecisionDto
            {
                RequestId = requestId.ToString(),
                RequestorId = "requestor-1",
                Outcome = "Allocated",
                SlotId = "M1-2",
            }],
            []);

        await activity.RunAsync(null!, input);

        bookingRepo.Verify(r => r.UpdateBookingRequestStatusAsync(
            TenantId, requestId, "Allocated",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            "M1-2",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PersistDecisionsInput MakeInput(Guid requestId, string outcome) =>
        new(DrawKey, TenantId, "2026-06-02",
            [new DrawDecisionDto { RequestId = requestId.ToString(), RequestorId = "requestor-1", Outcome = outcome }],
            []);
}
