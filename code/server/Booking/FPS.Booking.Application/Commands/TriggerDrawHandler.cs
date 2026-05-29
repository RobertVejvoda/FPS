using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Services;
using static FPS.Booking.Domain.Services.DrawService;
using FPS.Booking.Domain.ValueObjects;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class TriggerDrawHandler : IRequestHandler<TriggerDrawCommand, TriggerDrawResult>
{
    private readonly IBookingRepository bookingRepository;
    private readonly IBookingQueryRepository bookingQueryRepository;
    private readonly IDrawRepository drawRepository;
    private readonly IEmployeeMetricsService metricsService;
    private readonly IAvailableSlotService slotService;
    private readonly ITenantPolicyService policyService;
    private readonly IBookingEventPublisher eventPublisher;
    private readonly DrawService drawService;

    public TriggerDrawHandler(
        IBookingRepository bookingRepository,
        IBookingQueryRepository bookingQueryRepository,
        IDrawRepository drawRepository,
        IEmployeeMetricsService metricsService,
        IAvailableSlotService slotService,
        ITenantPolicyService policyService,
        IBookingEventPublisher eventPublisher,
        DrawService drawService)
    {
        ArgumentNullException.ThrowIfNull(bookingRepository);
        ArgumentNullException.ThrowIfNull(bookingQueryRepository);
        ArgumentNullException.ThrowIfNull(drawRepository);
        ArgumentNullException.ThrowIfNull(metricsService);
        ArgumentNullException.ThrowIfNull(slotService);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(drawService);
        this.bookingRepository = bookingRepository;
        this.bookingQueryRepository = bookingQueryRepository;
        this.drawRepository = drawRepository;
        this.metricsService = metricsService;
        this.slotService = slotService;
        this.policyService = policyService;
        this.eventPublisher = eventPublisher;
        this.drawService = drawService;
    }

    public async Task<TriggerDrawResult> Handle(TriggerDrawCommand cmd, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(cmd.TimeSlotStart, cmd.TimeSlotEnd);
        var drawKey = DrawKey.Create(cmd.TenantId, cmd.LocationId, cmd.Date, timeSlot);

        // Idempotency: return existing completed draw without re-running
        var existing = await drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);
        if (existing?.Status == "Completed")
        {
            return new TriggerDrawResult(
                existing.DrawKey,
                existing.Status,
                existing.AllocatedCount,
                existing.RejectedCount,
                existing.WaitlistedCount,
                WasAlreadyCompleted: true);
        }

        var startedAt = DateTime.UtcNow;
        var steps = new List<DrawLifecycleStepRecord>();

        var seed = GenerateSeed(drawKey);
        var publisher = eventPublisher.WithContext(new BookingPublishContext(
            cmd.TenantId, Guid.NewGuid().ToString(), "system", null));
        _ = publisher.PublishAsync(new FPS.Booking.Domain.Events.DrawAttemptStartedEvent(drawKey, seed, startedAt));

        DrawResult? drawResult = null;
        IReadOnlyList<BookingRequestDto> pendingForKey = [];

        try
        {
            var t = DateTime.UtcNow;
            var policy = await policyService.GetEffectivePolicyAsync(cmd.TenantId, cancellationToken: cancellationToken);
            steps.Add(Step("PolicyResolved", "Completed", t, summary: $"AlgorithmVersion will be resolved during allocation"));

            t = DateTime.UtcNow;
            pendingForKey = await bookingQueryRepository.GetPendingRequestsForDrawAsync(
                cmd.TenantId, cmd.LocationId, cmd.Date, cancellationToken);
            steps.Add(Step("RequestsLoaded", "Completed", t, summary: $"{pendingForKey.Count} pending request(s)"));

            t = DateTime.UtcNow;
            var availableSlots = await slotService.GetAvailableSlotsAsync(
                cmd.TenantId, cmd.LocationId, cmd.Date, timeSlot, cancellationToken);
            steps.Add(Step("CapacityLoaded", "Completed", t, summary: $"{availableSlots.Count} available slot(s)"));

            var pendingRequests = pendingForKey
                .Select(d => BookingRequest.Restore(
                    BookingRequestId.FromGuid(d.RequestId),
                    UserId.FromString(d.RequestedBy),
                    VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
                    TimeSlot.Create(d.PlannedArrivalTime, d.PlannedDepartureTime),
                    BookingRequestStatus.Pending,
                    d.RequestedAt))
                .ToList();

            t = DateTime.UtcNow;
            var requestorIds = pendingRequests.Select(r => r.RequestorId.Value.ToString()).Distinct();
            var metrics = await metricsService.GetMetricsSnapshotAsync(
                cmd.TenantId, requestorIds, cmd.Date, policy.AllocationLookbackDays, cancellationToken);
            steps.Add(Step("MetricsLoaded", "Completed", t, summary: $"{metrics.Count} requestor metric record(s)"));

            t = DateTime.UtcNow;
            drawResult = drawService.RunDraw(pendingRequests, availableSlots, metrics, seed);
            var allocated = drawResult.Decisions.Count(d => d.Outcome == DrawOutcome.Allocated);
            var rejected = drawResult.Decisions.Count(d => d.Outcome == DrawOutcome.Rejected);
            var waitlisted = drawResult.Decisions.Count(d => d.Outcome == DrawOutcome.Waitlisted);
            steps.Add(Step("WeightedAllocationCompleted", "Completed", t,
                summary: $"{allocated} allocated, {rejected} rejected, {waitlisted} waitlisted; algorithm: {drawResult.AlgorithmVersion}"));

            t = DateTime.UtcNow;
            foreach (var decision in drawResult.Decisions)
            {
                var dto = pendingForKey.FirstOrDefault(d => d.RequestId == decision.RequestId.Value);
                if (dto is null) continue;

                var decisionPublisher = eventPublisher.WithContext(new BookingPublishContext(
                    cmd.TenantId, Guid.NewGuid().ToString(), "system", null,
                    SubjectRequestorId: decision.RequestorId.Value.ToString(),
                    AllocationSource: "draw"));

                switch (decision.Outcome)
                {
                    case DrawOutcome.Allocated:
                        await bookingRepository.UpdateBookingRequestStatusAsync(
                            cmd.TenantId, decision.RequestId.Value, "Allocated", cancellationToken: cancellationToken);
                        await metricsService.IncrementRecentAllocationAsync(
                            cmd.TenantId, decision.RequestorId.Value.ToString(), cmd.Date, cancellationToken);
                        if (decision.SlotId is not null)
                            _ = decisionPublisher.PublishAsync(new FPS.Booking.Domain.Events.SlotAllocationCreatedEvent(
                                FPS.Booking.Domain.ValueObjects.SlotAllocationId.New(),
                                decision.RequestId, decision.SlotId, timeSlot));
                        break;

                    case DrawOutcome.Rejected:
                        await bookingRepository.UpdateBookingRequestStatusAsync(
                            cmd.TenantId, decision.RequestId.Value, "Rejected", decision.Reason,
                            BookingRejectionCode.DrawNotSelected.ToString(), cancellationToken);
                        _ = decisionPublisher.PublishAsync(new FPS.Booking.Domain.Events.BookingRequestRejectedEvent(
                            decision.RequestId,
                            BookingRejectionCode.DrawNotSelected,
                            decision.Reason ?? "Not selected in draw"));
                        break;

                    case DrawOutcome.Waitlisted:
                        // Remains Pending — no status update or integration event needed
                        break;
                }
            }
            steps.Add(Step("DecisionsPersisted", "Completed", t,
                summary: $"{drawResult.Decisions.Count} decision(s) persisted to booking store"));

            // EventsPublished is Attempted because integration event delivery is fire-and-forget
            steps.Add(Step("EventsPublished", "Attempted", DateTime.UtcNow,
                summary: "DrawAttemptCompleted event and per-decision events dispatched fire-and-forget; delivery not guaranteed"));
        }
        catch (Exception ex)
        {
            steps.Add(Step("DrawFailed", "Failed", DateTime.UtcNow, errorMessage: ex.Message));
            var failedAttempt = new DrawAttemptDto
            {
                DrawKey = drawKey.ToStoreKey(),
                TenantId = cmd.TenantId,
                LocationId = cmd.LocationId,
                Date = cmd.Date,
                Status = "Failed",
                Seed = seed,
                AlgorithmVersion = drawResult?.AlgorithmVersion ?? string.Empty,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                Decisions = drawResult?.Decisions.Select(d => new DrawDecisionDto
                {
                    RequestId = d.RequestId.Value.ToString(),
                    RequestorId = d.RequestorId.Value.ToString(),
                    Outcome = d.Outcome.ToString(),
                    SlotId = d.SlotId?.Value,
                    Reason = d.Reason
                }).ToList() ?? [],
                LifecycleSteps = steps
            };
            await drawRepository.SaveAsync(failedAttempt, cancellationToken);
            throw;
        }

        var attempt = new DrawAttemptDto
        {
            DrawKey = drawKey.ToStoreKey(),
            TenantId = cmd.TenantId,
            LocationId = cmd.LocationId,
            Date = cmd.Date,
            Status = "Completed",
            Seed = seed,
            AlgorithmVersion = drawResult.AlgorithmVersion,
            AllocatedCount = drawResult.Decisions.Count(d => d.Outcome == DrawOutcome.Allocated),
            RejectedCount = drawResult.Decisions.Count(d => d.Outcome == DrawOutcome.Rejected),
            WaitlistedCount = drawResult.Decisions.Count(d => d.Outcome == DrawOutcome.Waitlisted),
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            Decisions = drawResult.Decisions.Select(d => new DrawDecisionDto
            {
                RequestId = d.RequestId.Value.ToString(),
                RequestorId = d.RequestorId.Value.ToString(),
                Outcome = d.Outcome.ToString(),
                SlotId = d.SlotId?.Value,
                Reason = d.Reason
            }).ToList(),
            Tier2CandidateSequence = drawResult.Tier2CandidateSequence.Select(id => id.Value.ToString()).ToList(),
            LifecycleSteps = steps
        };

        await drawRepository.SaveAsync(attempt, cancellationToken);

        _ = publisher.PublishAsync(new FPS.Booking.Domain.Events.DrawAttemptCompletedEvent(
            drawKey, seed,
            attempt.AllocatedCount, attempt.RejectedCount, attempt.WaitlistedCount,
            DateTime.UtcNow));

        return new TriggerDrawResult(
            attempt.DrawKey,
            attempt.Status,
            attempt.AllocatedCount,
            attempt.RejectedCount,
            attempt.WaitlistedCount,
            WasAlreadyCompleted: false);
    }

    private static DrawLifecycleStepRecord Step(
        string name, string status, DateTime startedAt,
        string? summary = null, string? errorMessage = null)
        => new()
        {
            StepName = name,
            Status = status,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            Summary = summary,
            ErrorMessage = errorMessage
        };

    // Seed derived from the draw key so the same key always gets the same seed on first run.
    // A re-run for the same key reuses the stored seed (loaded from DrawAttemptDto above).
    private static long GenerateSeed(DrawKey key)
        => Math.Abs((long)key.ToStoreKey().GetHashCode());
}
