using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Entities;
using FPS.Booking.Domain.Exceptions;
using FPS.Booking.Domain.Services;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class CancelBookingHandler : IRequestHandler<CancelBookingCommand, CancelBookingResult>
{
    private readonly IBookingRepository repository;
    private readonly IBookingQueryRepository queryRepository;
    private readonly IPenaltyRepository penaltyRepository;
    private readonly IDrawRepository drawRepository;
    private readonly ITenantPolicyService policyService;
    private readonly IEventPublisher eventPublisher;
    private readonly DrawService drawService;

    public CancelBookingHandler(
        IBookingRepository repository,
        IBookingQueryRepository queryRepository,
        IPenaltyRepository penaltyRepository,
        IDrawRepository drawRepository,
        ITenantPolicyService policyService,
        IEventPublisher eventPublisher,
        DrawService drawService)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(penaltyRepository);
        ArgumentNullException.ThrowIfNull(drawRepository);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(drawService);
        this.repository = repository;
        this.queryRepository = queryRepository;
        this.penaltyRepository = penaltyRepository;
        this.drawRepository = drawRepository;
        this.policyService = policyService;
        this.eventPublisher = eventPublisher;
        this.drawService = drawService;
    }

    public async Task<CancelBookingResult> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var dto = await repository.GetBookingRequestAsync(command.RequestId);
        if (dto is null) throw new BookingNotFoundException(command.RequestId);

        var wasAllocated = dto.Status == "Allocated";
        var request = RestoreRequest(dto);

        request.Cancel(command.Reason, eventPublisher);

        await repository.UpdateBookingRequestStatusAsync(
            command.RequestId, request.Status.ToString(), command.Reason, cancellationToken);

        if (wasAllocated)
        {
            await ApplyPenaltyAsync(dto, command, cancellationToken);
            await TryReallocateAsync(dto, command, cancellationToken);
        }

        return new CancelBookingResult(command.RequestId, request.Status.ToString());
    }

    private async Task ApplyPenaltyAsync(BookingRequestDto dto, CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var policy = await policyService.GetEffectivePolicyAsync(command.TenantId, cancellationToken: cancellationToken);
        var sourceEventId = $"cancel:{command.RequestId}:LateCancellation";

        if (await penaltyRepository.ExistsAsync(command.RequestId, "LateCancellation", cancellationToken))
            return; // idempotent

        var penalty = Penalty.Create(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            PenaltyType.LateCancellation,
            score: policy.LateCancellationPenalty,
            effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow),
            expiryDays: policy.AllocationLookbackDays,
            sourceEventId: sourceEventId);

        await penaltyRepository.SaveAsync(new PenaltyDto
        {
            Id = penalty.Id,
            RequestId = dto.RequestId,
            RequestorId = dto.RequestedBy,
            Type = "LateCancellation",
            Score = penalty.Score,
            EffectiveDate = penalty.EffectiveDate,
            ExpiryDate = penalty.ExpiryDate,
            SourceEventId = sourceEventId
        }, cancellationToken);

        _ = eventPublisher.PublishAsync(new FPS.Booking.Domain.Events.PenaltyAppliedEvent(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            PenaltyType.LateCancellation,
            penalty.Score,
            sourceEventId));
    }

    private async Task TryReallocateAsync(BookingRequestDto cancelledDto, CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(cancelledDto.PlannedArrivalTime);
        var timeSlot = TimeSlot.Create(cancelledDto.PlannedArrivalTime, cancelledDto.PlannedDepartureTime);

        var candidates = await queryRepository.GetPendingRequestsForDrawAsync(
            command.TenantId, cancelledDto.LocationId ?? string.Empty, date, cancellationToken);

        if (candidates.Count == 0) return;

        var releasedSlot = cancelledDto.AllocatedSlotId.HasValue
            ? AvailableSlot.Create(ParkingSlotId.FromString(cancelledDto.AllocatedSlotId.Value.ToString()))
            : null;

        if (releasedSlot is null) return;

        // Use original Draw ordering when available
        var drawKey = DrawKey.Create(command.TenantId, cancelledDto.LocationId ?? string.Empty, date, timeSlot);
        var drawAttempt = await drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);

        BookingRequestDto? winner = null;

        if (drawAttempt?.Tier2CandidateSequence is { Count: > 0 })
        {
            winner = drawAttempt.Tier2CandidateSequence
                .Select(id => candidates.FirstOrDefault(c => c.RequestId.ToString() == id))
                .FirstOrDefault(c => c is not null && releasedSlot.CanAccommodate(
                    VehicleInformation.Create("X", VehicleType.Sedan, false, false, false)));
        }

        // Fallback: pick first compatible candidate
        winner ??= candidates.FirstOrDefault(c =>
            releasedSlot.CanAccommodate(
                VehicleInformation.Create("X", VehicleType.Sedan, false, false, false)));

        if (winner is null) return;

        var winnerRequest = RestoreRequest(winner);
        winnerRequest.Allocate(eventPublisher);

        await repository.UpdateBookingRequestStatusAsync(winner.RequestId, "Allocated", cancellationToken: cancellationToken);

        _ = eventPublisher.PublishAsync(new FPS.Booking.Domain.Events.BookingRequestReallocatedEvent(
            BookingRequestId.FromGuid(winner.RequestId),
            UserId.FromString(winner.RequestedBy),
            releasedSlot.SlotId,
            BookingRequestId.FromGuid(cancelledDto.RequestId)));
    }

    private static BookingRequest RestoreRequest(BookingRequestDto dto)
        => BookingRequest.Restore(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
            TimeSlot.Create(dto.PlannedArrivalTime, dto.PlannedDepartureTime),
            Enum.Parse<BookingRequestStatus>(dto.Status),
            dto.RequestedAt);
}
