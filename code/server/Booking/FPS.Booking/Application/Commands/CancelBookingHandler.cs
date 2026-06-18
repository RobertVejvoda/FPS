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
using FPS.SharedKernel.Time;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class CancelBookingHandler : IRequestHandler<CancelBookingCommand, CancelBookingResult>
{
    private readonly IBookingRepository repository;
    private readonly IBookingQueryRepository queryRepository;
    private readonly IPenaltyRepository penaltyRepository;
    private readonly IDrawRepository drawRepository;
    private readonly ITenantPolicyService policyService;
    private readonly IBookingEventPublisher eventPublisher;
    private readonly DrawService drawService;
    private readonly IAvailableSlotService slotService;
    private readonly ISystemClock clock;

    public CancelBookingHandler(
        IBookingRepository repository,
        IBookingQueryRepository queryRepository,
        IPenaltyRepository penaltyRepository,
        IDrawRepository drawRepository,
        ITenantPolicyService policyService,
        IBookingEventPublisher eventPublisher,
        DrawService drawService,
        IAvailableSlotService slotService,
        ISystemClock clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queryRepository);
        ArgumentNullException.ThrowIfNull(penaltyRepository);
        ArgumentNullException.ThrowIfNull(drawRepository);
        ArgumentNullException.ThrowIfNull(policyService);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(drawService);
        ArgumentNullException.ThrowIfNull(slotService);
        ArgumentNullException.ThrowIfNull(clock);
        this.repository = repository;
        this.queryRepository = queryRepository;
        this.penaltyRepository = penaltyRepository;
        this.drawRepository = drawRepository;
        this.policyService = policyService;
        this.eventPublisher = eventPublisher;
        this.slotService = slotService;
        this.drawService = drawService;
        this.clock = clock;
    }

    public async Task<CancelBookingResult> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var dto = await repository.GetBookingRequestAsync(command.TenantId, command.RequestId);
        if (dto is null) throw new BookingNotFoundException(command.RequestId);
        if (!string.IsNullOrEmpty(dto.TenantId) && dto.TenantId != command.TenantId)
            throw new BookingNotFoundException(command.RequestId);

        var wasAllocated = dto.Status == "Allocated";
        var request = RestoreRequest(dto);

        var publisher = eventPublisher.WithContext(new BookingPublishContext(
            command.TenantId, Guid.NewGuid().ToString(), command.ActorType, command.RequestorId,
            SubjectRequestorId: dto.RequestedBy));
        request.Cancel(command.Reason, publisher);

        await repository.UpdateBookingRequestStatusAsync(
            command.TenantId, command.RequestId, request.Status.ToString(), command.Reason, cancellationToken: cancellationToken);

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

        if (await penaltyRepository.ExistsAsync(command.TenantId, command.RequestId, "LateCancellation", cancellationToken))
            return; // idempotent

        var penalty = Penalty.Create(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            PenaltyType.LateCancellation,
            score: policy.LateCancellationPenalty,
            effectiveDate: DateOnly.FromDateTime(clock.GetTenantUtcNow(command.TenantId).UtcDateTime),
            expiryDays: policy.AllocationLookbackDays,
            sourceEventId: sourceEventId);

        await penaltyRepository.SaveAsync(new PenaltyDto
        {
            Id = penalty.Id,
            RequestId = dto.RequestId,
            TenantId = command.TenantId,
            RequestorId = dto.RequestedBy,
            Type = "LateCancellation",
            Score = penalty.Score,
            EffectiveDate = penalty.EffectiveDate,
            ExpiryDate = penalty.ExpiryDate,
            SourceEventId = sourceEventId
        }, cancellationToken);

        _ = eventPublisher.WithContext(new BookingPublishContext(
            command.TenantId, Guid.NewGuid().ToString(), "system", null))
            .PublishAsync(new FPS.Booking.Domain.Events.PenaltyAppliedEvent(
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
        var locationId = cancelledDto.LocationId ?? string.Empty;

        var candidates = await queryRepository.GetPendingRequestsForDrawAsync(
            command.TenantId, locationId, date, cancellationToken);

        if (candidates.Count == 0) return;
        if (cancelledDto.AllocatedSlotId is not { } releasedSlotId) return;

        // Re-resolve the released slot's full capabilities (IsMotorcycleCapacity,
        // HasCharger, IsAccessible, IsCompanyCarReserved) by looking it up from
        // the slot service. Reconstructing from the id alone would lose those
        // flags and let, e.g., a cancelled motorcycle unit ("M1-1") be
        // reallocated to a sedan — violating the v1 motorcycle-only rule.
        var allSlots = await slotService.GetAvailableSlotsAsync(
            command.TenantId, locationId, date, timeSlot, cancellationToken);
        var releasedSlot = allSlots.FirstOrDefault(s => s.SlotId.Value == releasedSlotId)
            ?? AvailableSlot.Create(ParkingSlotId.FromString(releasedSlotId));

        // Use original Draw ordering when available
        var drawKey = DrawKey.Create(command.TenantId, locationId, date, timeSlot);
        var drawAttempt = await drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);

        BookingRequestDto? winner = null;

        if (drawAttempt?.Tier2CandidateSequence is { Count: > 0 })
        {
            winner = drawAttempt.Tier2CandidateSequence
                .Select(id => candidates.FirstOrDefault(c => c.RequestId.ToString() == id))
                .FirstOrDefault(c => c is not null && releasedSlot.CanAccommodate(VehicleFromDto(c)));
        }

        // Fallback: pick first compatible candidate using each candidate's persisted vehicle facts.
        winner ??= candidates.FirstOrDefault(c => releasedSlot.CanAccommodate(VehicleFromDto(c)));

        if (winner is null) return;

        var winnerRequest = RestoreRequest(winner);
        var reallocationPublisher = eventPublisher.WithContext(new BookingPublishContext(
            command.TenantId, Guid.NewGuid().ToString(), "system", null,
            SubjectRequestorId: winner.RequestedBy,
            AllocationSource: "reallocation"));
        winnerRequest.Allocate(reallocationPublisher);

        // Persist the actual released slot reference (e.g. "M1-1") onto the winner —
        // without this, the reallocated booking would lose its capacity link, same
        // bug class as the previous Codex finding on PersistDecisionsActivity.
        await repository.UpdateBookingRequestStatusAsync(
            command.TenantId, winner.RequestId, "Allocated",
            allocatedSlotId: releasedSlot.SlotId.Value,
            cancellationToken: cancellationToken);

        _ = reallocationPublisher.PublishAsync(new FPS.Booking.Domain.Events.BookingRequestReallocatedEvent(
            BookingRequestId.FromGuid(winner.RequestId),
            UserId.FromString(winner.RequestedBy),
            releasedSlot.SlotId,
            BookingRequestId.FromGuid(cancelledDto.RequestId)));
    }

    // Build VehicleInformation from the candidate's persisted facts so reallocation
    // honours motorcycle-only / EV-charger / accessibility constraints. Falls back
    // to Sedan for older dtos that predate the VehicleType persistence change.
    private static VehicleInformation VehicleFromDto(BookingRequestDto dto)
        => VehicleInformation.Create(
            "REALLOC",
            Enum.TryParse<VehicleType>(dto.VehicleType, ignoreCase: true, out var vt) ? vt : VehicleType.Sedan,
            dto.VehicleIsElectric,
            dto.RequiresAccessibleSpot,
            dto.VehicleIsCompanyCar);

    private static BookingRequest RestoreRequest(BookingRequestDto dto)
        => BookingRequest.Restore(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            VehicleFromDto(dto),
            TimeSlot.Create(dto.PlannedArrivalTime, dto.PlannedDepartureTime),
            Enum.Parse<BookingRequestStatus>(dto.Status),
            dto.RequestedAt);
}
