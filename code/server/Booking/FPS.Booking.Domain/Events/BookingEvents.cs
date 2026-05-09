using FPS.SharedKernel.DomainEvents;
using FPS.Booking.Domain.ValueObjects;
using System;

namespace FPS.Booking.Domain.Events;

// BookingRequest events
public record BookingRequestCreatedEvent(
    BookingRequestId RequestId,
    UserId RequestorId,
    TimeSlot RequestedPeriod) : DomainEvent;

public record BookingRequestAcceptedEvent(
    BookingRequestId RequestId) : DomainEvent;

public record BookingRequestRejectedEvent(
    BookingRequestId RequestId,
    string Reason) : DomainEvent;

public record BookingRequestCancelledEvent(
    BookingRequestId RequestId,
    string Reason) : DomainEvent;

// SlotAllocation events
public record SlotAllocationCreatedEvent(
    SlotAllocationId AllocationId,
    BookingRequestId RequestId,
    ParkingSlotId SlotId,
    TimeSlot Period) : DomainEvent;

public record SlotUsageStartedEvent(
    SlotAllocationId AllocationId,
    DateTime StartTime) : DomainEvent;

public record SlotUsageCompletedEvent(
    SlotAllocationId AllocationId,
    DateTime EndTime) : DomainEvent;

public record SlotAllocationCancelledEvent(
    SlotAllocationId AllocationId,
    string Reason) : DomainEvent;

public class BookingDeclinedEvent
{
    public Guid RequestId { get; set; }
    public string Reason { get; set; }
    public DateTime DeclinedAt { get; set; }
}

public class AllocationConfirmedEvent
{
    public Guid RequestId { get; set; }
    public Guid AllocationId { get; set; }
    public Guid SlotId { get; set; }
    public DateTime ConfirmedAt { get; set; }
}

public class ArrivalConfirmedEvent
{
    public Guid AllocationId { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public string ConfirmedBy { get; set; }
}

public class ReservationExpiredEvent
{
    public Guid AllocationId { get; set; }
    public Guid RequestId { get; set; }
    public DateTime ExpiredAt { get; set; }
}

public class ReservationCancelledEvent
{
    public Guid AllocationId { get; set; }
    public Guid RequestId { get; set; }
    public string Reason { get; set; }
    public string CancelledBy { get; set; }
    public DateTime CancelledAt { get; set; }
}