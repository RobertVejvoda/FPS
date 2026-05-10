using FPS.Booking.Application.Exceptions;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Exceptions;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class CancelBookingHandler : IRequestHandler<CancelBookingCommand, CancelBookingResult>
{
    private readonly IBookingRepository repository;
    private readonly IEventPublisher eventPublisher;

    public CancelBookingHandler(IBookingRepository repository, IEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        this.repository = repository;
        this.eventPublisher = eventPublisher;
    }

    public async Task<CancelBookingResult> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var dto = await repository.GetBookingRequestAsync(command.RequestId);

        if (dto is null)
            throw new BookingNotFoundException(command.RequestId);

        var request = BookingRequest.Restore(
            BookingRequestId.FromGuid(dto.RequestId),
            UserId.FromString(dto.RequestedBy),
            VehicleInformation.Create("UNKNOWN", VehicleType.Sedan, false, false, false),
            TimeSlot.Create(dto.PlannedArrivalTime, dto.PlannedDepartureTime),
            Enum.Parse<BookingRequestStatus>(dto.Status),
            dto.RequestedAt);

        request.Cancel(command.Reason, eventPublisher);

        await repository.UpdateBookingRequestStatusAsync(command.RequestId, request.Status.ToString(), command.Reason);

        return new CancelBookingResult(command.RequestId, request.Status.ToString());
    }
}
