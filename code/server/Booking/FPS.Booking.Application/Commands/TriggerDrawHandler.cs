using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Services;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
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
    private readonly IEventPublisher eventPublisher;
    private readonly DrawService drawService;

    public TriggerDrawHandler(
        IBookingRepository bookingRepository,
        IBookingQueryRepository bookingQueryRepository,
        IDrawRepository drawRepository,
        IEmployeeMetricsService metricsService,
        IAvailableSlotService slotService,
        ITenantPolicyService policyService,
        IEventPublisher eventPublisher,
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

        var policy = await policyService.GetEffectivePolicyAsync(cmd.TenantId, cancellationToken: cancellationToken);

        var pendingForKey = await bookingQueryRepository.GetPendingRequestsForDrawAsync(
            cmd.TenantId, cmd.LocationId, cmd.Date, cancellationToken);

        var pendingRequests = pendingForKey
            .Select(d => BookingRequest.Restore(
                BookingRequestId.FromGuid(d.RequestId),
                UserId.FromString(d.RequestedBy),
                VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
                TimeSlot.Create(d.PlannedArrivalTime, d.PlannedDepartureTime),
                BookingRequestStatus.Pending,
                d.RequestedAt))
            .ToList();

        var availableSlots = await slotService.GetAvailableSlotsAsync(
            cmd.TenantId, cmd.LocationId, cmd.Date, timeSlot, cancellationToken);

        var requestorIds = pendingRequests.Select(r => r.RequestorId.Value.ToString()).Distinct();
        var metrics = await metricsService.GetMetricsSnapshotAsync(
            cmd.TenantId, requestorIds, cmd.Date, policy.AllocationLookbackDays, cancellationToken);

        var seed = GenerateSeed(drawKey);
        _ = eventPublisher.PublishAsync(new FPS.Booking.Domain.Events.DrawAttemptStartedEvent(drawKey, seed, DateTime.UtcNow));

        var result = drawService.RunDraw(pendingRequests, availableSlots, metrics, seed);

        // Persist decisions and update metrics
        foreach (var decision in result.Decisions)
        {
            var dto = pendingForKey.FirstOrDefault(d => d.RequestId == decision.RequestId.Value);
            if (dto is null) continue;

            switch (decision.Outcome)
            {
                case DrawOutcome.Allocated:
                    await bookingRepository.UpdateBookingRequestStatusAsync(
                        decision.RequestId.Value, "Allocated", cancellationToken: cancellationToken);
                    await metricsService.IncrementRecentAllocationAsync(
                        cmd.TenantId, decision.RequestorId.Value.ToString(), cmd.Date, cancellationToken);
                    break;

                case DrawOutcome.Rejected:
                    await bookingRepository.UpdateBookingRequestStatusAsync(
                        decision.RequestId.Value, "Rejected", decision.Reason, cancellationToken);
                    break;

                case DrawOutcome.Waitlisted:
                    // Remains Pending — no status update needed
                    break;
            }
        }

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
            Tier2CandidateSequence = result.Tier2CandidateSequence.Select(id => id.Value.ToString()).ToList()
        };

        await drawRepository.SaveAsync(attempt, cancellationToken);

        _ = eventPublisher.PublishAsync(new FPS.Booking.Domain.Events.DrawAttemptCompletedEvent(
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

    // Seed derived from the draw key so the same key always gets the same seed on first run.
    // A re-run for the same key reuses the stored seed (loaded from DrawAttemptDto above).
    private static long GenerateSeed(DrawKey key)
        => Math.Abs((long)key.ToStoreKey().GetHashCode());
}
