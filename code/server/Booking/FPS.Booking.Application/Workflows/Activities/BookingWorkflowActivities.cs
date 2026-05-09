using System;
using System.Threading.Tasks;
using Dapr.Workflow;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.Models;

namespace FPS.Booking.Application.Workflows.Activities
{
    public class BookingWorkflowActivities
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IAllocationService _allocationService;
        private readonly IEventPublisher _eventPublisher;

        public BookingWorkflowActivities(
            IBookingRepository bookingRepository,
            IAllocationService allocationService,
            IEventPublisher eventPublisher)
        {
            _bookingRepository = bookingRepository;
            _allocationService = allocationService;
            _eventPublisher = eventPublisher;
        }

        [Activity("RecordBookingRequest")]
        public async Task RecordBookingRequestAsync(BookingRequestDto request)
        {
            await _bookingRepository.CreateBookingRequestAsync(request);
        }

        [Activity("CheckFacilityAvailability")]
        public async Task<FacilityAvailabilityResult> CheckFacilityAvailabilityAsync(BookingRequestDto request)
        {
            return await _allocationService.CheckFacilityAvailabilityAsync(
                request.FacilityId,
                request.PlannedArrivalTime,
                request.PlannedDepartureTime);
        }

        [Activity("FindAvailableSlot")]
        public async Task<SlotAllocationResult> FindAvailableSlotAsync(BookingRequestDto request)
        {
            return await _allocationService.FindAvailableSlotAsync(
                request.FacilityId,
                request.VehicleId,
                request.PlannedArrivalTime,
                request.PlannedDepartureTime);
        }

        [Activity("RecordAllocation")]
        public async Task<AllocationDto> RecordAllocationAsync(SlotAllocationResult slotResult)
        {
            var allocation = new AllocationDto
            {
                AllocationId = Guid.NewGuid(),
                RequestId = slotResult.RequestId,
                SlotId = slotResult.SlotId,
                Status = "Reserved",
                AllocatedAt = DateTime.UtcNow
            };

            await _bookingRepository.CreateAllocationAsync(allocation);
            return allocation;
        }

        [Activity("PublishBookingDeclinedEvent")]
        public async Task PublishBookingDeclinedEventAsync(BookingDeclinedEvent @event)
        {
            await _eventPublisher.PublishEventAsync("booking.declined", @event);
        }

        [Activity("PublishAllocationConfirmedEvent")]
        public async Task PublishAllocationConfirmedEventAsync(AllocationConfirmedEvent @event)
        {
            await _eventPublisher.PublishEventAsync("allocation.confirmed", @event);
        }

        [Activity("UpdateAllocationWithArrival")]
        public async Task UpdateAllocationWithArrivalAsync(UpdateAllocationInfo info)
        {
            await _bookingRepository.UpdateAllocationArrivalAsync(
                info.AllocationId,
                info.ArrivalTime,
                info.ConfirmedBy);
        }

        [Activity("PublishArrivalConfirmedEvent")]
        public async Task PublishArrivalConfirmedEventAsync(ArrivalConfirmedEvent @event)
        {
            await _eventPublisher.PublishEventAsync("arrival.confirmed", @event);
        }

        [Activity("ExpireReservation")]
        public async Task ExpireReservationAsync(Guid allocationId)
        {
            await _bookingRepository.UpdateAllocationStatusAsync(allocationId, "Expired");
        }

        [Activity("PublishReservationExpiredEvent")]
        public async Task PublishReservationExpiredEventAsync(ReservationExpiredEvent @event)
        {
            await _eventPublisher.PublishEventAsync("reservation.expired", @event);
        }

        [Activity("CancelReservation")]
        public async Task CancelReservationAsync(CancellationInfo info)
        {
            await _bookingRepository.UpdateAllocationStatusAsync(
                info.AllocationId,
                "Cancelled",
                info.Reason);
        }

        [Activity("PublishReservationCancelledEvent")]
        public async Task PublishReservationCancelledEventAsync(ReservationCancelledEvent @event)
        {
            await _eventPublisher.PublishEventAsync("reservation.cancelled", @event);
        }
    }
}