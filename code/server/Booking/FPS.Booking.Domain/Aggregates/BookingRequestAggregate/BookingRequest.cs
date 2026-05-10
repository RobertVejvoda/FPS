using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.Exceptions;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;
using FPS.SharedKernel.Interfaces;

namespace FPS.Booking.Domain.Aggregates.BookingRequestAggregate;

public sealed class BookingRequest : IAggregateRoot
{
    public BookingRequestId Id { get; private set; }
    public UserId RequestorId { get; private set; }
    public VehicleInformation Vehicle { get; private set; }
    public TimeSlot RequestedPeriod { get; private set; }
    public BookingRequestStatus Status { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public BookingRejectionCode? RejectionCode { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? CancellationReason { get; private set; }

    private static readonly BookingRequestStatus[] TerminalStatuses =
        [BookingRequestStatus.Rejected, BookingRequestStatus.Cancelled,
         BookingRequestStatus.Used, BookingRequestStatus.NoShow, BookingRequestStatus.Expired];

    private BookingRequest() { }

    // Submits a new booking request. Validates synchronously — ends in Pending or Rejected.
    public static BookingRequest Submit(
        UserId requestorId,
        TimeSlot requestedPeriod,
        VehicleInformation vehicle,
        SubmissionContext context,
        IEventPublisher eventPublisher)
    {
        var request = new BookingRequest
        {
            Id = BookingRequestId.New(),
            RequestorId = requestorId,
            Vehicle = vehicle,
            RequestedPeriod = requestedPeriod,
            Status = BookingRequestStatus.Submitted,
            SubmittedAt = DateTime.UtcNow
        };

        eventPublisher.PublishAsync(new BookingRequestSubmittedEvent(
            request.Id, requestorId, requestedPeriod));

        var (rejectionCode, rejectionReason) = request.Validate(context);

        if (rejectionCode is not null)
        {
            request.Status = BookingRequestStatus.Rejected;
            request.RejectionCode = rejectionCode;
            request.RejectionReason = rejectionReason;
            eventPublisher.PublishAsync(new BookingRequestRejectedEvent(request.Id, rejectionCode.Value, rejectionReason!));
        }
        else
        {
            request.Status = BookingRequestStatus.Pending;
            eventPublisher.PublishAsync(new BookingRequestPendingEvent(request.Id));
        }

        return request;
    }

    // Reconstructs the aggregate from persisted state. No events fired.
    public static BookingRequest Restore(
        BookingRequestId id,
        UserId requestorId,
        VehicleInformation vehicle,
        TimeSlot requestedPeriod,
        BookingRequestStatus status,
        DateTime submittedAt,
        BookingRejectionCode? rejectionCode = null,
        string? rejectionReason = null,
        string? cancellationReason = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(requestorId);

        return new BookingRequest
        {
            Id = id,
            RequestorId = requestorId,
            Vehicle = vehicle,
            RequestedPeriod = requestedPeriod,
            Status = status,
            SubmittedAt = submittedAt,
            RejectionCode = rejectionCode,
            RejectionReason = rejectionReason,
            CancellationReason = cancellationReason
        };
    }

    // Called by Draw or same-day allocation when a slot is assigned.
    public void Allocate(IEventPublisher eventPublisher)
    {
        if (Status != BookingRequestStatus.Pending)
            throw new BookingException("Only pending requests can be allocated");

        Status = BookingRequestStatus.Allocated;
        eventPublisher.PublishAsync(new BookingRequestAllocatedEvent(Id));
    }

    // Called by Draw when no slot is available for an already-pending request.
    public void Reject(BookingRejectionCode code, string reason, IEventPublisher eventPublisher)
    {
        if (Status != BookingRequestStatus.Pending)
            throw new BookingException("Only pending requests can be rejected");

        Status = BookingRequestStatus.Rejected;
        RejectionCode = code;
        RejectionReason = reason;
        eventPublisher.PublishAsync(new BookingRequestRejectedEvent(Id, code, reason));
    }

    public void Cancel(string reason, IEventPublisher eventPublisher)
    {
        if (Status != BookingRequestStatus.Pending && Status != BookingRequestStatus.Allocated)
            throw new BookingException("Only pending or allocated requests can be cancelled");

        Status = BookingRequestStatus.Cancelled;
        CancellationReason = reason;
        eventPublisher.PublishAsync(new BookingRequestCancelledEvent(Id, reason));
    }

    public bool IsTerminal() => TerminalStatuses.Contains(Status);

    private (BookingRejectionCode? code, string? reason) Validate(SubmissionContext context)
    {
        if (RequestedPeriod.Start.Date < DateTime.UtcNow.Date)
            return (BookingRejectionCode.PastDate, "Cannot submit a request for a date in the past.");

        if (context.IsCutOffPassed)
            return (BookingRejectionCode.CutOffPassed, "Requests for this time slot are closed.");

        if (context.IsCapExceeded)
            return (BookingRejectionCode.DailyCapExceeded, "The daily request cap for this date has been reached.");

        if (context.HasOverlappingRequest)
            return (BookingRejectionCode.DuplicateRequest, "You already have a request for an overlapping time slot.");

        return (null, null);
    }
}
