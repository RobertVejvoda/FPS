using System;
using System.Collections.Generic;
using FPS.SharedKernel.Interfaces;
using FPS.SharedKernel.DomainEvents;
using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Domain.Aggregates.SlotAllocationAggregate;

public class SlotAllocation : IAggregateRoot
{
    public SlotAllocationId Id { get; private set; }
    public BookingRequestId BookingRequestId { get; private set; }
    public ParkingSlotId SlotId { get; private set; }
    public TimeSlot Period { get; private set; }
    public SlotAllocationStatus Status { get; private set; }
    public DateTime? UsageStartTime { get; private set; }
    public DateTime? UsageEndTime { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For deserialization
    private SlotAllocation()
    {
        Id = null!;
        BookingRequestId = null!;
        SlotId = null!;
        Period = null!;
    }
    
    private SlotAllocation(
        SlotAllocationId id,
        BookingRequestId bookingRequestId,
        ParkingSlotId slotId,
        TimeSlot period)
    {
        Id = id;
        BookingRequestId = bookingRequestId;
        SlotId = slotId;
        Period = period;
        Status = SlotAllocationStatus.Reserved;
        CreatedAt = DateTime.UtcNow;
    }

    public static SlotAllocation CreateAllocation(
        BookingRequestId bookingRequestId,
        ParkingSlotId slotId,
        TimeSlot period,
        IEventPublisher eventPublisher)
    {
        var allocation = new SlotAllocation(
            SlotAllocationId.New(),
            bookingRequestId,
            slotId,
            period
        );

        // Publish domain event
        eventPublisher.PublishAsync(new SlotAllocationCreatedEvent(
            allocation.Id,
            bookingRequestId,
            slotId,
            period));

        return allocation;
    }

    public void StartUsage(DateTime startTime, IEventPublisher eventPublisher)
    {
        if (Status != SlotAllocationStatus.Reserved)
            throw new BookingException("Only reserved slots can be used");

        if (!Period.Contains(startTime))
            throw new BookingException("Start time must be within the allocated period");

        Status = SlotAllocationStatus.InUse;
        UsageStartTime = startTime;

        // Publish domain event
        eventPublisher.PublishAsync(new SlotUsageStartedEvent(Id, startTime));
    }

    public void CompleteUsage(DateTime endTime, IEventPublisher eventPublisher)
    {
        if (Status != SlotAllocationStatus.InUse)
            throw new BookingException("Only slots in use can be completed");

        if (UsageStartTime == null)
            throw new BookingException("Cannot complete usage without a start time");

        if (endTime < UsageStartTime.Value)
            throw new BookingException("End time cannot be before start time");

        Status = SlotAllocationStatus.Completed;
        UsageEndTime = endTime;

        // Publish domain event
        eventPublisher.PublishAsync(new SlotUsageCompletedEvent(Id, endTime));
    }

    public void Cancel(string reason, IEventPublisher eventPublisher)
    {
        if (Status == SlotAllocationStatus.Completed)
            throw new BookingException("Completed allocations cannot be cancelled");

        Status = SlotAllocationStatus.Cancelled;

        // Publish domain event
        eventPublisher.PublishAsync(new SlotAllocationCancelledEvent(Id, reason));
    }
}