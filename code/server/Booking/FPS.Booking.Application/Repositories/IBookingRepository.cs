using FPS.Booking.Application.Models;
using FPS.Booking.Domain.ValueObjects;

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
    Task<int> CountRequestsForDateAsync(string tenantId, DateTime date, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingRequestAsync(string tenantId, string requestorId, TimeSlot period, CancellationToken cancellationToken = default);
}
