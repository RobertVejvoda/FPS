using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Exceptions;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class ConfirmSlotUsageHandler : IRequestHandler<ConfirmSlotUsageCommand, ConfirmSlotUsageResult>
{
    private readonly IBookingRepository repository;
    private readonly IEventPublisher eventPublisher;

    public ConfirmSlotUsageHandler(IBookingRepository repository, IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        this.repository = repository;
        this.eventPublisher = eventPublisher;
    }

    public async Task<ConfirmSlotUsageResult> Handle(ConfirmSlotUsageCommand command, CancellationToken cancellationToken)
    {
        var dto = await repository.GetBookingRequestAsync(command.RequestId);
        if (dto is null) throw new BookingNotFoundException(command.RequestId);

        var confirmedAt = command.ConfirmedAt ?? DateTime.UtcNow;
        var source = Enum.Parse<ConfirmationSource>(command.ConfirmationSource, ignoreCase: true);

        // Idempotency: already Used with same source → return existing state
        if (dto.Status == "Used")
        {
            return new ConfirmSlotUsageResult(
                command.RequestId,
                "Used",
                dto.UsageConfirmedAt ?? confirmedAt,
                WasAlreadyConfirmed: true);
        }

        var request = BookingRequest.Restore(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
            TimeSlot.Create(dto.PlannedArrivalTime, dto.PlannedDepartureTime),
            Enum.Parse<BookingRequestStatus>(dto.Status),
            dto.RequestedAt);

        request.ConfirmUsage(source, confirmedAt, eventPublisher);

        await repository.UpdateBookingRequestUsageAsync(
            command.RequestId,
            command.ConfirmationSource,
            confirmedAt,
            command.SourceEventId,
            cancellationToken);

        return new ConfirmSlotUsageResult(command.RequestId, "Used", confirmedAt, WasAlreadyConfirmed: false);
    }
}
