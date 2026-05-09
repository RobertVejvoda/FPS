using System;
using System.Threading.Tasks;
using Dapr.Workflow;
using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.Models;
using FPS.Booking.Application.Services;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Application.Workflows
{
    public class BookingWorkflow : Workflow<BookingRequestDto, AllocatedSlotDto>
    {
        private readonly IAllocationService _allocationService;
        private readonly IBookingRepository _bookingRepository;
        private readonly IEventPublisher _eventPublisher;

        public BookingWorkflow(
            IAllocationService allocationService,
            IBookingRepository bookingRepository,
            IEventPublisher eventPublisher)
        {
            _allocationService = allocationService;
            _bookingRepository = bookingRepository;
            _eventPublisher = eventPublisher;
        }

        public override async Task<AllocatedSlotDto> RunAsync(WorkflowContext context, BookingRequestDto request)
        {
            // Step 1: Record the booking request
            await context.CallActivityAsync(
                "RecordBookingRequest",
                request);

            // Set custom status to show progress
            context.SetCustomStatus("RequestRecorded");

            // Step 2: Check facility availability
            var facilityResult = await context.CallActivityAsync<FacilityAvailabilityResult>(
                "CheckFacilityAvailability",
                request);

            if (!facilityResult.IsAvailable)
            {
                // Publish event about facility unavailability
                await context.CallActivityAsync(
                    "PublishBookingDeclinedEvent",
                    new BookingDeclinedEvent
                    {
                        RequestId = request.RequestId,
                        Reason = "Facility unavailable: " + facilityResult.Reason,
                        DeclinedAt = DateTime.UtcNow
                    });

                // Return null to indicate no allocation was made
                return null;
            }

            // Update status
            context.SetCustomStatus("FacilityAvailabilityConfirmed");

            // Step 3: Find available slot
            var slotResult = await context.CallActivityAsync<SlotAllocationResult>(
                "FindAvailableSlot",
                request);

            if (!slotResult.Success)
            {
                // Publish event about allocation failure
                await context.CallActivityAsync(
                    "PublishBookingDeclinedEvent",
                    new BookingDeclinedEvent
                    {
                        RequestId = request.RequestId,
                        Reason = "No available slots: " + slotResult.Reason,
                        DeclinedAt = DateTime.UtcNow
                    });

                // Return null to indicate no allocation was made
                return null;
            }

            // Update status
            context.SetCustomStatus("SlotAllocated");

            // Step 4: Record the allocation
            var allocation = await context.CallActivityAsync<AllocationDto>(
                "RecordAllocation",
                slotResult);

            // Step 5: Publish allocation confirmed event
            await context.CallActivityAsync(
                "PublishAllocationConfirmedEvent",
                new AllocationConfirmedEvent
                {
                    RequestId = request.RequestId,
                    AllocationId = allocation.AllocationId,
                    SlotId = slotResult.SlotId,
                    ConfirmedAt = DateTime.UtcNow
                });

            // Wait for arrival or cancellation
            using (var timeoutCts = new CancellationTokenSource())
            {
                // Set timeout for the arrival (e.g., 30 minutes after planned arrival)
                var timeout = request.PlannedArrivalTime.AddMinutes(30) - DateTime.UtcNow;
                if (timeout.TotalMilliseconds > 0)
                {
                    timeoutCts.CancelAfter(timeout);
                }
                else
                {
                    // Already past the arrival time window, skip waiting
                    timeoutCts.Cancel();
                }

                // Wait for events (arrival confirmation, cancellation, or timeout)
                try
                {
                    context.SetCustomStatus("WaitingForArrival");

                    // Wait for the driver arrival confirmation event
                    var arrivalConfirmation = await context.WaitForExternalEventAsync<ArrivalConfirmationDto>(
                        "DriverArrivalConfirmed",
                        timeoutCts.Token);

                    // Driver has arrived, update the allocation
                    await context.CallActivityAsync(
                        "UpdateAllocationWithArrival",
                        new UpdateAllocationInfo
                        {
                            AllocationId = allocation.AllocationId,
                            ArrivalTime = arrivalConfirmation.ArrivalTime,
                            ConfirmedBy = arrivalConfirmation.ConfirmedBy
                        });

                    // Publish the arrival confirmed event
                    await context.CallActivityAsync(
                        "PublishArrivalConfirmedEvent",
                        new ArrivalConfirmedEvent
                        {
                            AllocationId = allocation.AllocationId,
                            ConfirmedAt = arrivalConfirmation.ArrivalTime,
                            ConfirmedBy = arrivalConfirmation.ConfirmedBy
                        });

                    context.SetCustomStatus("ArrivalConfirmed");
                }
                catch (TaskCanceledException)
                {
                    // Timeout occurred - driver did not arrive within the expected window
                    // Mark the reservation as expired
                    await context.CallActivityAsync(
                        "ExpireReservation",
                        allocation.AllocationId);

                    // Publish the expiration event
                    await context.CallActivityAsync(
                        "PublishReservationExpiredEvent",
                        new ReservationExpiredEvent
                        {
                            AllocationId = allocation.AllocationId,
                            RequestId = request.RequestId,
                            ExpiredAt = DateTime.UtcNow
                        });

                    context.SetCustomStatus("ReservationExpired");
                }
                catch (Exception)
                {
                    // Handle other errors
                    throw;
                }
            }

            // Listen for cancellation event
            try
            {
                // Create a task that completes when the cancellation event is received
                var cancellationTask = context.WaitForExternalEventAsync<ReservationCancellationDto>(
                    "ReservationCancelled");

                // If a cancellation is received, process it
                if (await Task.WhenAny(cancellationTask, Task.Delay(Timeout.Infinite)) == cancellationTask)
                {
                    var cancellationInfo = cancellationTask.Result;

                    // Cancel the reservation
                    await context.CallActivityAsync(
                        "CancelReservation",
                        new CancellationInfo
                        {
                            AllocationId = allocation.AllocationId,
                            Reason = cancellationInfo.CancellationReason,
                            CancelledBy = cancellationInfo.CancelledBy,
                            CancellationTime = cancellationInfo.CancellationTime
                        });

                    // Publish the cancellation event
                    await context.CallActivityAsync(
                        "PublishReservationCancelledEvent",
                        new ReservationCancelledEvent
                        {
                            AllocationId = allocation.AllocationId,
                            RequestId = request.RequestId,
                            Reason = cancellationInfo.CancellationReason,
                            CancelledBy = cancellationInfo.CancelledBy,
                            CancelledAt = cancellationInfo.CancellationTime
                        });

                    context.SetCustomStatus("ReservationCancelled");
                }
            }
            catch (Exception)
            {
                // Handle any errors
                throw;
            }

            // Return the allocated slot information
            return new AllocatedSlotDto
            {
                AllocationId = allocation.AllocationId,
                SlotId = allocation.SlotId,
                FacilityId = request.FacilityId,
                StartTime = request.PlannedArrivalTime,
                EndTime = request.PlannedDepartureTime
            };
        }
    }
}