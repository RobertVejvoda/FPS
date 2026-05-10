using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Entities;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class EvaluateNoShowHandler : IRequestHandler<EvaluateNoShowCommand, EvaluateNoShowResult>
{
    private readonly IBookingRepository repository;
    private readonly IBookingQueryRepository queryRepository;
    private readonly IPenaltyRepository penaltyRepository;
    private readonly ITenantPolicyService policyService;
    private readonly IEventPublisher eventPublisher;

    public EvaluateNoShowHandler(
        IBookingRepository repository,
        IBookingQueryRepository queryRepository,
        IPenaltyRepository penaltyRepository,
        ITenantPolicyService policyService,
        IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(penaltyRepository);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        this.repository = repository;
        this.queryRepository = queryRepository;
        this.penaltyRepository = penaltyRepository;
        this.policyService = policyService;
        this.eventPublisher = eventPublisher;
    }

    public async Task<EvaluateNoShowResult> Handle(EvaluateNoShowCommand command, CancellationToken cancellationToken)
    {
        var policy = await policyService.GetEffectivePolicyAsync(command.TenantId, cancellationToken: cancellationToken);

        // FPS must never mark no-show when confirmation is unavailable or detection is disabled
        if (!policy.UsageConfirmationEnabled)
            return new EvaluateNoShowResult(0, 0, "Usage confirmation is not enabled for this tenant.");

        if (!policy.NoShowDetectionEnabled)
            return new EvaluateNoShowResult(0, 0, "No-show detection is not enabled for this tenant.");

        var allocatedRequests = await queryRepository.GetAllocatedRequestsForDrawAsync(
            command.TenantId, command.LocationId, command.Date, cancellationToken);

        var markedCount = 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var dto in allocatedRequests)
        {
            // Skip if already has a usage confirmation
            if (!string.IsNullOrEmpty(dto.ConfirmationSource)) continue;

            var request = BookingRequest.Restore(
                BookingRequestId.FromGuid(dto.RequestId),
                UserId.FromString(dto.RequestedBy),
                VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
                TimeSlot.Create(dto.PlannedArrivalTime, dto.PlannedDepartureTime),
                BookingRequestStatus.Allocated,
                dto.RequestedAt);

            request.MarkNoShow(eventPublisher);

            await repository.UpdateBookingRequestStatusAsync(
                dto.RequestId, "NoShow", command.Reason, cancellationToken);

            await ApplyNoShowPenaltyAsync(dto, policy, today, cancellationToken);

            markedCount++;
        }

        return new EvaluateNoShowResult(markedCount, allocatedRequests.Count - markedCount, null);
    }

    private async Task ApplyNoShowPenaltyAsync(
        BookingRequestDto dto, TenantPolicy policy, DateOnly today, CancellationToken cancellationToken)
    {
        var sourceEventId = $"noshow:{dto.RequestId}:NoShow";

        if (await penaltyRepository.ExistsAsync(dto.RequestId, "NoShow", cancellationToken))
            return; // idempotent

        var penalty = Penalty.Create(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            PenaltyType.NoShow,
            score: policy.NoShowPenalty,
            effectiveDate: today,
            expiryDays: policy.AllocationLookbackDays,
            sourceEventId: sourceEventId);

        await penaltyRepository.SaveAsync(new PenaltyDto
        {
            Id = penalty.Id,
            RequestId = dto.RequestId,
            RequestorId = dto.RequestedBy,
            Type = "NoShow",
            Score = penalty.Score,
            EffectiveDate = penalty.EffectiveDate,
            ExpiryDate = penalty.ExpiryDate,
            SourceEventId = sourceEventId
        }, cancellationToken);

        _ = eventPublisher.PublishAsync(new FPS.Booking.Domain.Events.PenaltyAppliedEvent(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            PenaltyType.NoShow,
            penalty.Score,
            sourceEventId));
    }
}
