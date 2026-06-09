using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Aggregates.SlotAllocationAggregate;
using FPS.Booking.Domain.Interfaces;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;

namespace FPS.Booking.Domain.Services;

public class ParkingAllocationService
{
    private readonly ISlotAllocationRepository _slotAllocationRepository;
    private readonly IEventPublisher _eventPublisher;

    public ParkingAllocationService(
        ISlotAllocationRepository slotAllocationRepository,
        IEventPublisher eventPublisher)
    {
        _slotAllocationRepository = slotAllocationRepository ?? throw new ArgumentNullException(nameof(slotAllocationRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>
    /// Checks if a parking slot is available for a given time period.
    /// </summary>
    public async Task<bool> IsSlotAvailableForPeriodAsync(ParkingSlotId slotId, TimeSlot period)
    {
        try
        {
            var existingAllocations = await _slotAllocationRepository.GetBySlotIdForPeriodAsync(slotId, period);

            return !existingAllocations.Any(a =>
                a.Status != SlotAllocationStatus.Cancelled &&
                a.Period.Overlaps(period));
        }
        catch (Exception ex)
        {
            throw new BookingException($"Failed to check availability for slot {slotId}", ex);
        }
    }

    /// <summary>
    /// Allocates a parking slot for a booking request.
    /// </summary>
    public async Task<SlotAllocation> AllocateSlotAsync(BookingRequest request, ParkingSlotId slotId)
    {
        if (request.Status != BookingRequestStatus.Pending)
            throw new BookingException("Cannot allocate slots for non-pending requests");

        var isAvailable = await IsSlotAvailableForPeriodAsync(slotId, request.RequestedPeriod);

        if (!isAvailable)
            throw new BookingException($"Slot {slotId} is not available for the requested period");

        // Create the allocation and publish the event
        var allocation = SlotAllocation.CreateAllocation(
            request.Id,
            slotId,
            request.RequestedPeriod,
            _eventPublisher);

        try
        {
            await _slotAllocationRepository.AddAsync(allocation);
        }
        catch (Exception ex)
        {
            throw new BookingException($"Failed to allocate slot {slotId} for booking request {request.Id}", ex);
        }

        return allocation;
    }

    /// <summary>
    /// Validates if allocation is feasible for a booking request.
    /// </summary>
    public async Task<bool> ValidateAllocationFeasibilityAsync(BookingRequest request)
    {
        if (request.Status != BookingRequestStatus.Pending)
            throw new BookingException("Cannot validate feasibility for non-pending requests");

        try
        {
            // Check if any slots are available for the requested period
            var availableSlots = await _slotAllocationRepository.GetActiveAllocationsAsync();

            return availableSlots.Any(a =>
                a.Status == SlotAllocationStatus.Reserved &&
                a.Period.Overlaps(request.RequestedPeriod));
        }
        catch (Exception ex)
        {
            throw new BookingException($"Failed to validate allocation feasibility for booking request {request.Id}", ex);
        }
    }
}