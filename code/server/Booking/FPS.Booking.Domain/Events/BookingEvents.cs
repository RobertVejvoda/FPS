using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;

namespace FPS.Booking.Domain.Events;

// Usage confirmation event
public record BookingRequestUsedEvent(
    BookingRequestId RequestId,
    ConfirmationSource Source,
    DateTime ConfirmedAt) : DomainEvent;

// Penalty events
public record PenaltyAppliedEvent(
    BookingRequestId RequestId,
    UserId RequestorId,
    PenaltyType PenaltyType,
    int Score,
    string SourceEventId) : DomainEvent;

// Reallocation event
public record BookingRequestReallocatedEvent(
    BookingRequestId NewRequestId,
    UserId NewRequestorId,
    ParkingSlotId SlotId,
    BookingRequestId OriginalCancelledRequestId) : DomainEvent;

// BookingRequest lifecycle events
public record BookingRequestSubmittedEvent(
    BookingRequestId RequestId,
    UserId RequestorId,
    TimeSlot RequestedPeriod) : DomainEvent;

public record BookingRequestPendingEvent(
    BookingRequestId RequestId) : DomainEvent;

public record BookingRequestAllocatedEvent(
    BookingRequestId RequestId) : DomainEvent;

public record BookingRequestRejectedEvent(
    BookingRequestId RequestId,
    BookingRejectionCode RejectionCode,
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

// Draw events
public record DrawAttemptStartedEvent(
    DrawKey DrawKey,
    long Seed,
    DateTime StartedAt) : DomainEvent;

public record DrawAttemptCompletedEvent(
    DrawKey DrawKey,
    long Seed,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    DateTime CompletedAt) : DomainEvent;

// kept for backwards compat with existing workflow
public record DrawRunEvent(
    DateOnly DrawDate,
    int TotalRequests,
    int AllocatedCount,
    int RejectedCount) : DomainEvent;
