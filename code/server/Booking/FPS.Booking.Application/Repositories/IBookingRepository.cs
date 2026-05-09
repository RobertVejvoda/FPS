using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Repositories;

public interface IBookingRepository
{
    Task CreateBookingRequestAsync(BookingRequestDto request);
    Task<BookingRequestDto?> GetBookingRequestAsync(Guid requestId);
    Task CreateAllocationAsync(AllocationDto allocation);
    Task<AllocationDto?> GetAllocationAsync(Guid allocationId);
    Task<IEnumerable<AllocationDto>> GetAllocationsByStatusAsync(string status);
    Task<IEnumerable<AllocationDto>> GetAllocationsByFacilityAsync(Guid facilityId, DateTime from, DateTime to);
    Task UpdateAllocationStatusAsync(Guid allocationId, string status, string? reason = null);
    Task UpdateAllocationArrivalAsync(Guid allocationId, DateTime arrivalTime, string confirmedBy);
    Task UpdateAllocationDepartureAsync(Guid allocationId, DateTime departureTime, string confirmedBy);
}
