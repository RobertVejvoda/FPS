using System;
using System.Linq;
using System.Threading.Tasks;
using Dapr.Client;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Models;

namespace FPS.Booking.Infrastructure.Services
{
    public class DaprAllocationService : IAllocationService
    {
        private readonly DaprClient _daprClient;
        private readonly IBookingRepository _bookingRepository;
        private const string FACILITY_SERVICE = "facility-service";
        private const string SLOT_SERVICE = "slot-service";

        public DaprAllocationService(DaprClient daprClient, IBookingRepository bookingRepository)
        {
            _daprClient = daprClient;
            _bookingRepository = bookingRepository;
        }

        public async Task<FacilityAvailabilityResult> CheckFacilityAvailabilityAsync(
            Guid facilityId, 
            DateTime startTime, 
            DateTime endTime)
        {
            // Call the facility service to check basic availability
            var facilityStatus = await _daprClient.InvokeMethodAsync<object, FacilityStatusDto>(
                HttpMethod.Get,
                FACILITY_SERVICE,
                $"api/facilities/{facilityId}/status",
                null);

            if (facilityStatus == null || facilityStatus.Status != "Open")
            {
                return new FacilityAvailabilityResult
                {
                    IsAvailable = false,
                    Reason = facilityStatus?.Reason ?? "Facility not found or unavailable"
                };
            }

            // Check operating hours
            var operatingHours = await _daprClient.InvokeMethodAsync<object, OperatingHoursDto>(
                HttpMethod.Get,
                FACILITY_SERVICE,
                $"api/facilities/{facilityId}/operating-hours?date={startTime.Date:yyyy-MM-dd}",
                null);

            if (operatingHours == null)
            {
                return new FacilityAvailabilityResult
                {
                    IsAvailable = false,
                    Reason = "Operating hours information not found"
                };
            }

            var startTimeLocal = startTime.ToLocalTime();
            var endTimeLocal = endTime.ToLocalTime();

            if (startTimeLocal.TimeOfDay < operatingHours.OpenTime || 
                endTimeLocal.TimeOfDay > operatingHours.CloseTime)
            {
                return new FacilityAvailabilityResult
                {
                    IsAvailable = false,
                    Reason = $"Requested time is outside facility operating hours ({operatingHours.OpenTime} - {operatingHours.CloseTime})"
                };
            }

            // Check for facility maintenance or closures
            var maintenanceEvents = await _daprClient.InvokeMethodAsync<object, MaintenanceEventDto[]>(
                HttpMethod.Get,
                FACILITY_SERVICE,
                $"api/facilities/{facilityId}/maintenance?from={startTime:yyyy-MM-ddTHH:mm:ss}&to={endTime:yyyy-MM-ddTHH:mm:ss}",
                null);

            if (maintenanceEvents != null && maintenanceEvents.Length > 0)
            {
                var conflictingEvent = maintenanceEvents.FirstOrDefault(e => 
                    (e.StartTime <= startTime && e.EndTime >= startTime) ||
                    (e.StartTime <= endTime && e.EndTime >= endTime) ||
                    (e.StartTime >= startTime && e.EndTime <= endTime));

                if (conflictingEvent != null)
                {
                    return new FacilityAvailabilityResult
                    {
                        IsAvailable = false,
                        Reason = $"Facility maintenance scheduled: {conflictingEvent.Description}"
                    };
                }
            }

            return new FacilityAvailabilityResult
            {
                IsAvailable = true
            };
        }

        public async Task<SlotAllocationResult> FindAvailableSlotAsync(
            Guid facilityId, 
            Guid vehicleId, 
            DateTime startTime, 
            DateTime endTime)
        {
            // Get vehicle details to determine required slot type
            var vehicleDetails = await _daprClient.InvokeMethodAsync<object, VehicleDetailsDto>(
                HttpMethod.Get,
                "vehicle-service",
                $"api/vehicles/{vehicleId}",
                null);

            if (vehicleDetails == null)
            {
                return new SlotAllocationResult
                {
                    Success = false,
                    Reason = "Vehicle details not found"
                };
            }

            // Get all slots at the facility
            var facilitySlots = await _daprClient.InvokeMethodAsync<object, ParkingSlotDto[]>(
                HttpMethod.Get,
                SLOT_SERVICE,
                $"api/facilities/{facilityId}/slots?slotType={vehicleDetails.VehicleType}",
                null);

            if (facilitySlots == null || !facilitySlots.Any())
            {
                return new SlotAllocationResult
                {
                    Success = false,
                    Reason = $"No suitable slots found for vehicle type: {vehicleDetails.VehicleType}"
                };
            }

            // Get existing allocations for this facility in the time range
            var existingAllocations = await _bookingRepository.GetAllocationsByFacilityAsync(
                facilityId, startTime, endTime);

            // Find a free slot
            foreach (var slot in facilitySlots)
            {
                // Skip slots under maintenance
                if (slot.Status != "Available")
                {
                    continue;
                }

                // Check if slot is already allocated
                var isAllocated = existingAllocations.Any(a => 
                    a.SlotId == slot.SlotId && 
                    a.Status != "Cancelled" && 
                    a.Status != "Expired" &&
                    ((a.StartTime <= startTime && a.EndTime >= startTime) ||
                     (a.StartTime <= endTime && a.EndTime >= endTime) ||
                     (a.StartTime >= startTime && a.EndTime <= endTime)));

                if (!isAllocated)
                {
                    // Slot is free, return it
                    return new SlotAllocationResult
                    {
                        Success = true,
                        SlotId = slot.SlotId,
                        FacilityId = facilityId,
                        StartTime = startTime,
                        EndTime = endTime,
                        VehicleId = vehicleId
                    };
                }
            }

            // No free slots found
            return new SlotAllocationResult
            {
                Success = false,
                Reason = "All suitable slots are currently booked"
            };
        }
    }
}