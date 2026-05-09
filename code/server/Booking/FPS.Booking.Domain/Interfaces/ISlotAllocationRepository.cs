using FPS.Booking.Domain.Aggregates.SlotAllocationAggregate;

namespace FPS.Booking.Domain.Interfaces;

public interface ISlotAllocationRepository
{
    Task<SlotAllocation> GetByIdAsync(SlotAllocationId id);
    Task<SlotAllocation> GetByBookingRequestIdAsync(BookingRequestId bookingRequestId);
    Task<IEnumerable<SlotAllocation>> GetBySlotIdForPeriodAsync(ParkingSlotId slotId, TimeSlot period);
    Task<IEnumerable<SlotAllocation>> GetByUserIdAsync(UserId userId);
    Task<IEnumerable<SlotAllocation>> GetActiveAllocationsAsync();
    Task AddAsync(SlotAllocation slotAllocation);
    Task UpdateAsync(SlotAllocation slotAllocation);
}