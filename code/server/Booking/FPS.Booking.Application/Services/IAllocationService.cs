using System;
using System.Threading.Tasks;
using FPS.Booking.Domain.Models;

namespace FPS.Booking.Application.Services
{
    public interface IAllocationService
    {
        Task<FacilityAvailabilityResult> CheckFacilityAvailabilityAsync(
            Guid facilityId, 
            DateTime startTime, 
            DateTime endTime);
            
        Task<SlotAllocationResult> FindAvailableSlotAsync(
            Guid facilityId, 
            Guid vehicleId, 
            DateTime startTime, 
            DateTime endTime);
    }
}