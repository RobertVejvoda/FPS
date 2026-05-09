using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;

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

public record SlotAllocationConfirmedEvent(
    SlotAllocationId AllocationId,
    BookingRequestId RequestId,
    ParkingSlotId SlotId) : DomainEvent;

public record SlotAllocationExpiredEvent(
    SlotAllocationId AllocationId) : DomainEvent;

public record SlotAllocationCancelledEvent(
    SlotAllocationId AllocationId,
    string Reason) : DomainEvent;

public record SlotUsageStartedEvent(
    SlotAllocationId AllocationId,
    DateTime StartTime) : DomainEvent;

public record SlotUsageCompletedEvent(
    SlotAllocationId AllocationId,
    DateTime EndTime) : DomainEvent;
