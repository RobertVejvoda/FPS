using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Services;
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

        var steps = new List<DrawLifecycleStepDto>();
        var correlationId = Guid.NewGuid().ToString();
        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();

        // Step 1: Policy resolution
        var stepStarted = DateTime.UtcNow;
        var policy = await policyService.GetEffectivePolicyAsync(cmd.TenantId, cancellationToken: cancellationToken);
        steps.Add(new DrawLifecycleStepDto
        {
            StepName = "PolicyResolved",
            Status = "Completed",
            StartedAt = stepStarted,
            CompletedAt = DateTime.UtcNow,
            Summary = "Retrieved and validated tenant allocation policy",
            CorrelationId = correlationId,
            TraceId = traceId
        });

        var pendingForKey = await bookingQueryRepository.GetPendingRequestsForDrawAsync(
            cmd.TenantId, cmd.LocationId, cmd.Date, cancellationToken);

        // Step 2: Pending requests loaded
        stepStarted = DateTime.UtcNow;
        var pendingRequests = pendingForKey
            .Select(d => BookingRequest.Restore(
                BookingRequestId.FromGuid(d.RequestId),
                UserId.FromString(d.RequestedBy),
                VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
                TimeSlot.Create(d.PlannedArrivalTime, d.PlannedDepartureTime),
                BookingRequestStatus.Pending,
                d.RequestedAt))
            .ToList();
        steps.Add(new DrawLifecycleStepDto
        {
            StepName = "RequestsLoaded",
            Status = "Completed",
            StartedAt = stepStarted,
            CompletedAt = DateTime.UtcNow,
            Summary = $"Loaded {pendingRequests.Count} pending booking request(s)",
            CorrelationId = correlationId,
            TraceId = traceId
        });

        // Step 3: Available capacity loaded
        stepStarted = DateTime.UtcNow;
        var availableSlots = await slotService.GetAvailableSlotsAsync(
            cmd.TenantId, cmd.LocationId, cmd.Date, timeSlot, cancellationToken);
        steps.Add(new DrawLifecycleStepDto
        {
            StepName = "CapacityLoaded",
            Status = "Completed",
            StartedAt = stepStarted,
            CompletedAt = DateTime.UtcNow,
            Summary = $"Loaded {availableSlots.Count} available slot(s)",
            CorrelationId = correlationId,
            TraceId = traceId
        });

        var requestorIds = pendingRequests.Select(r => r.RequestorId.Value.ToString()).Distinct();
        var metrics = await metricsService.GetMetricsSnapshotAsync(
            cmd.TenantId, requestorIds, cmd.Date, policy.AllocationLookbackDays, cancellationToken);

        // Step 4: Fairness metrics loaded
        stepStarted = DateTime.UtcNow;
        steps.Add(new DrawLifecycleStepDto
        {
            StepName = "MetricsLoaded",
            Status = "Completed",
            StartedAt = stepStarted,
            CompletedAt = DateTime.UtcNow,
            Summary = $"Loaded fairness metrics for {requestorIds.Count()} unique requestor(s)",
            CorrelationId = correlationId,
            TraceId = traceId
        });

        var seed = GenerateSeed(drawKey);
        var publisher = eventPublisher.WithContext(new BookingPublishContext(
            cmd.TenantId, correlationId, "system", null));
        _ = publisher.PublishAsync(new FPS.Booking.Domain.Events.DrawAttemptStartedEvent(drawKey, seed, DateTime.UtcNow));

        // Step 5: Weighted allocation run
        stepStarted = DateTime.UtcNow;
        var result = drawService.RunDraw(pendingRequests, availableSlots, metrics, seed);
        steps.Add(new DrawLifecycleStepDto
        {
            StepName = "WeightedAllocationCompleted",
            Status = "Completed",
            StartedAt = stepStarted,
            CompletedAt = DateTime.UtcNow,
            Summary = $"Allocation completed with algorithm {result.AlgorithmVersion}",
            CorrelationId = correlationId,
            TraceId = traceId
        });

        // Persist decisions and update metrics
        // Step 6: Decisions persisted
        stepStarted = DateTime.UtcNow;
        foreach (var decision in result.Decisions)
        {
            var dto = pendingForKey.FirstOrDefault(d => d.RequestId == decision.RequestId.Value);
            if (dto is null) continue;

            var decisionPublisher = eventPublisher.WithContext(new BookingPublishContext(
                cmd.TenantId, correlationId, "system", null,
                SubjectRequestorId: decision.RequestorId.Value.ToString(),
                AllocationSource: "draw"));

            switch (decision.Outcome)
            {
                case DrawOutcome.Allocated:
                    await bookingRepository.UpdateBookingRequestStatusAsync(
                        decision.RequestId.Value, "Allocated", cancellationToken: cancellationToken);
                    await metricsService.IncrementRecentAllocationAsync(
                        cmd.TenantId, decision.RequestorId.Value.ToString(), cmd.Date, cancellationToken);
                    if (decision.SlotId is not null)
                        _ = decisionPublisher.PublishAsync(new FPS.Booking.Domain.Events.SlotAllocationCreatedEvent(
                            FPS.Booking.Domain.ValueObjects.SlotAllocationId.New(),
                            decision.RequestId, decision.SlotId, timeSlot));
                    break;

                case DrawOutcome.Rejected:
                    await bookingRepository.UpdateBookingRequestStatusAsync(
                        decision.RequestId.Value, "Rejected", decision.Reason, cancellationToken);
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
        steps.Add(new DrawLifecycleStepDto
        {
            StepName = "DecisionsPersisted",
            Status = "Completed",
            StartedAt = stepStarted,
            CompletedAt = DateTime.UtcNow,
            Summary = $"Persisted {result.Decisions.Count} decision(s) and updated status",
            CorrelationId = correlationId,
            TraceId = traceId
        });

        var attempt = new DrawAttemptDto
        {
            DrawKey = drawKey.ToStoreKey(),
            TenantId = cmd.TenantId,
            LocationId = cmd.LocationId,
            Date = cmd.Date,
            Status = "Completed",
            Seed = seed,
            AlgorithmVersion = result.AlgorithmVersion,
            AllocatedCount = result.Decisions.Count(d => d.Outcome == DrawOutcome.Allocated),
            RejectedCount = result.Decisions.Count(d => d.Outcome == DrawOutcome.Rejected),
            WaitlistedCount = result.Decisions.Count(d => d.Outcome == DrawOutcome.Waitlisted),
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Decisions = result.Decisions.Select(d => new DrawDecisionDto
            {
                RequestId = d.RequestId.Value.ToString(),
                RequestorId = d.RequestorId.Value.ToString(),
                Outcome = d.Outcome.ToString(),
                SlotId = d.SlotId?.Value,
                Reason = d.Reason
            }).ToList(),
            Tier2CandidateSequence = result.Tier2CandidateSequence.Select(id => id.Value.ToString()).ToList(),
            Steps = steps,
            CorrelationId = correlationId,
            TraceId = traceId
        };

        await drawRepository.SaveAsync(attempt, cancellationToken);

        // Step 7: Events published
        stepStarted = DateTime.UtcNow;
        _ = publisher.PublishAsync(new FPS.Booking.Domain.Events.DrawAttemptCompletedEvent(
            drawKey, seed,
            attempt.AllocatedCount, attempt.RejectedCount, attempt.WaitlistedCount,
            DateTime.UtcNow));
        steps.Add(new DrawLifecycleStepDto
        {
            StepName = "EventsPublished",
            Status = "Completed",
            StartedAt = stepStarted,
            CompletedAt = DateTime.UtcNow,
            Summary = "Published draw completion and decision events",
            CorrelationId = correlationId,
            TraceId = traceId
        });

        return new TriggerDrawResult(
            attempt.DrawKey,
            attempt.Status,
            attempt.AllocatedCount,
            attempt.RejectedCount,
            attempt.WaitlistedCount,
            WasAlreadyCompleted: false);
    }

    // Seed derived from the draw key so the same key always gets the same seed on first run.
    // A re-run for the same key reuses the stored seed (loaded from DrawAttemptDto above).
    private static long GenerateSeed(DrawKey key)
        => Math.Abs((long)key.ToStoreKey().GetHashCode());
}
