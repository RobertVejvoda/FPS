using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Client;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Models;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Infrastructure.Repositories
{
    public class DaprBookingRepository : IBookingRepository
    {
        private readonly DaprClient _daprClient;
        private const string BOOKING_STORE = "booking-store";

        public DaprBookingRepository(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }

        public async Task CreateBookingRequestAsync(BookingRequestDto request)
        {
            await _daprClient.SaveStateAsync(
                BOOKING_STORE,
                $"request:{request.RequestId}",
                request);
        }

        public async Task<BookingRequestDto> GetBookingRequestAsync(Guid requestId)
        {
            return await _daprClient.GetStateAsync<BookingRequestDto>(
                BOOKING_STORE,
                $"request:{requestId}");
        }

        public async Task CreateAllocationAsync(AllocationDto allocation)
        {
            await _daprClient.SaveStateAsync(
                BOOKING_STORE,
                $"allocation:{allocation.AllocationId}",
                allocation);

            // Also index by facility and time for querying
            var facilityIndex = new FacilityAllocationIndex
            {
                FacilityId = allocation.FacilityId,
                AllocationId = allocation.AllocationId,
                SlotId = allocation.SlotId,
                StartTime = allocation.StartTime,
                EndTime = allocation.EndTime
            };

            await _daprClient.SaveStateAsync(
                BOOKING_STORE,
                $"facility:{allocation.FacilityId}:allocation:{allocation.AllocationId}",
                facilityIndex);

            // Index by status
            var statusIndex = new StatusAllocationIndex
            {
                Status = allocation.Status,
                AllocationId = allocation.AllocationId
            };

            await _daprClient.SaveStateAsync(
                BOOKING_STORE,
                $"status:{allocation.Status}:allocation:{allocation.AllocationId}",
                statusIndex);
        }

        public async Task<AllocationDto> GetAllocationAsync(Guid allocationId)
        {
            return await _daprClient.GetStateAsync<AllocationDto>(
                BOOKING_STORE,
                $"allocation:{allocationId}");
        }

        public async Task<IEnumerable<AllocationDto>> GetAllocationsByStatusAsync(string status)
        {
            // This is simplistic and would need to be replaced with a proper query
            // in a production system using a query API or secondary index
            var allIndexes = await _daprClient.GetStateAsync<Dictionary<string, StatusAllocationIndex>>(
                BOOKING_STORE,
                $"status:{status}");

            var results = new List<AllocationDto>();
            
            if (allIndexes != null)
            {
                foreach (var index in allIndexes.Values)
                {
                    var allocation = await GetAllocationAsync(index.AllocationId);
                    if (allocation != null)
                    {
                        results.Add(allocation);
                    }
                }
            }

            return results;
        }

        public async Task<IEnumerable<AllocationDto>> GetAllocationsByFacilityAsync(
            Guid facilityId, DateTime from, DateTime to)
        {
            // This is simplistic and would need to be replaced with a proper query
            // in a production system using a query API or secondary index
            var allIndexes = await _daprClient.GetStateAsync<Dictionary<string, FacilityAllocationIndex>>(
                BOOKING_STORE,
                $"facility:{facilityId}");

            var results = new List<AllocationDto>();
            
            if (allIndexes != null)
            {
                foreach (var index in allIndexes.Values)
                {
                    // Filter by time range
                    if ((index.StartTime <= to && index.EndTime >= from))
                    {
                        var allocation = await GetAllocationAsync(index.AllocationId);
                        if (allocation != null)
                        {
                            results.Add(allocation);
                        }
                    }
                }
            }

            return results;
        }

        public async Task UpdateAllocationStatusAsync(Guid allocationId, string status, string reason = null)
        {
            var allocation = await GetAllocationAsync(allocationId);
            
            if (allocation != null)
            {
                // Handle old status index entry removal
                await _daprClient.DeleteStateAsync(
                    BOOKING_STORE,
                    $"status:{allocation.Status}:allocation:{allocationId}");

                // Update the allocation
                allocation.Status = status;
                allocation.StatusReason = reason;
                allocation.LastUpdated = DateTime.UtcNow;
                
                await _daprClient.SaveStateAsync(
                    BOOKING_STORE,
                    $"allocation:{allocationId}",
                    allocation);

                // Create new status index entry
                var statusIndex = new StatusAllocationIndex
                {
                    Status = status,
                    AllocationId = allocationId
                };

                await _daprClient.SaveStateAsync(
                    BOOKING_STORE,
                    $"status:{status}:allocation:{allocationId}",
                    statusIndex);
            }
        }

        public async Task UpdateAllocationArrivalAsync(Guid allocationId, DateTime arrivalTime, string confirmedBy)
        {
            var allocation = await GetAllocationAsync(allocationId);
            
            if (allocation != null)
            {
                allocation.Status = SlotAllocationStatus.InUse.ToString();
                allocation.ActualArrivalTime = arrivalTime;
                allocation.ArrivalConfirmedBy = confirmedBy;
                allocation.LastUpdated = DateTime.UtcNow;
                
                await _daprClient.SaveStateAsync(
                    BOOKING_STORE,
                    $"allocation:{allocationId}",
                    allocation);

                // Update status indexes
                await _daprClient.DeleteStateAsync(
                    BOOKING_STORE,
                    $"status:{allocation.Status}:allocation:{allocationId}");

                var statusIndex = new StatusAllocationIndex
                {
                    Status = "Active",
                    AllocationId = allocationId
                };

                await _daprClient.SaveStateAsync(
                    BOOKING_STORE,
                    $"status:Active:allocation:{allocationId}",
                    statusIndex);
            }
        }

        public async Task UpdateAllocationDepartureAsync(Guid allocationId, DateTime departureTime, string confirmedBy)
        {
            var allocation = await GetAllocationAsync(allocationId);
            
            if (allocation != null)
            {
                allocation.Status = "Completed";
                allocation.ActualDepartureTime = departureTime;
                allocation.DepartureConfirmedBy = confirmedBy;
                allocation.LastUpdated = DateTime.UtcNow;
                
                await _daprClient.SaveStateAsync(
                    BOOKING_STORE,
                    $"allocation:{allocationId}",
                    allocation);

                // Update status indexes
                await _daprClient.DeleteStateAsync(
                    BOOKING_STORE,
                    $"status:{allocation.Status}:allocation:{allocationId}");

                var statusIndex = new StatusAllocationIndex
                {
                    Status = "Completed",
                    AllocationId = allocationId
                };

                await _daprClient.SaveStateAsync(
                    BOOKING_STORE,
                    $"status:Completed:allocation:{allocationId}",
                    statusIndex);
            }
        }

        public async Task<int> CountRequestsForDateAsync(string tenantId, DateTime date, CancellationToken cancellationToken = default)
        {
            var key = $"count:{tenantId}:{date:yyyy-MM-dd}";
            var counter = await _daprClient.GetStateAsync<RequestDateCounter>(BOOKING_STORE, key, cancellationToken: cancellationToken);
            return counter?.Count ?? 0;
        }

        public async Task<bool> HasOverlappingRequestAsync(string tenantId, string requestorId, TimeSlot period, CancellationToken cancellationToken = default)
        {
            // Full overlap query requires MongoDB driver — Dapr state store does not support range queries.
            // Returns false until the MongoDB read-side implementation is added (infrastructure test phase).
            await Task.CompletedTask;
            return false;
        }

        // Index classes for state storage
        private class StatusAllocationIndex
        {
            public string Status { get; set; }
            public Guid AllocationId { get; set; }
        }

        private class RequestDateCounter
        {
            public int Count { get; set; }
        }

        private class FacilityAllocationIndex
        {
            public Guid FacilityId { get; set; }
            public Guid AllocationId { get; set; }
            public Guid SlotId { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }
    }
}