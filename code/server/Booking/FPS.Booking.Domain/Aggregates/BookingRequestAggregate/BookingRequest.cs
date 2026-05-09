using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.Exceptions;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using FPS.SharedKernel.Interfaces;

namespace FPS.Booking.Domain.Aggregates.BookingRequestAggregate;

public class BookingRequest : IAggregateRoot
{
    public BookingRequestId Id { get; private set; }
    public UserId RequestorId { get; private set; }
    public VehicleInformation Vehicle { get; private set; }
    public TimeSlot RequestedPeriod { get; private set; }
    public BookingRequestStatus Status { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? CancellationReason { get; private set; }

    private BookingRequest() { }

    public static BookingRequest Create(
        UserId requestorId,
        TimeSlot requestedPeriod,
        VehicleInformation vehicle,
        IEventPublisher eventPublisher)
    {
        if (requestedPeriod.Start.Date < DateTime.UtcNow.Date)
            throw new BookingException("Cannot create a booking request for a time period in the past");

        var request = new BookingRequest
        {
            Id = BookingRequestId.New(),
            RequestorId = requestorId,
            Vehicle = vehicle,
            RequestedPeriod = requestedPeriod,
            Status = BookingRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };

        eventPublisher.PublishAsync(new BookingRequestCreatedEvent(
            request.Id,
            requestorId,
            requestedPeriod));

        return request;
    }

    public void Accept(IEventPublisher eventPublisher)
    {
        if (Status != BookingRequestStatus.Pending)
            throw new BookingException("Only pending requests can be accepted");

        Status = BookingRequestStatus.Accepted;
        eventPublisher.PublishAsync(new BookingRequestAcceptedEvent(Id));
    }

    public void Reject(string reason, IEventPublisher eventPublisher)
    {
        if (Status != BookingRequestStatus.Pending)
            throw new BookingException("Only pending requests can be rejected");

        Status = BookingRequestStatus.Rejected;
        RejectionReason = reason;
        eventPublisher.PublishAsync(new BookingRequestRejectedEvent(Id, reason));
    }

    public void Cancel(string reason, IEventPublisher eventPublisher)
    {
        if (Status != BookingRequestStatus.Pending && Status != BookingRequestStatus.Accepted)
            throw new BookingException("Only pending or accepted requests can be cancelled");

        Status = BookingRequestStatus.Cancelled;
        CancellationReason = reason;
        eventPublisher.PublishAsync(new BookingRequestCancelledEvent(Id, reason));
    }
}
