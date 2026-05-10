using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Application.Workflows.Activities;
using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Workflows;

public class BookingWorkflow : Workflow<BookingRequestDto, AllocatedSlotDto?>
{
    public override async Task<AllocatedSlotDto?> RunAsync(WorkflowContext context, BookingRequestDto request)
    {
        // Step 1: Record the booking request
        await context.CallActivityAsync(
            nameof(RecordBookingRequestActivity),
            request);

        context.SetCustomStatus("BookingRequestRecorded");

        // Step 2: Check facility availability
        var facilityResult = await context.CallActivityAsync<FacilityAvailabilityResult>(
            nameof(CheckFacilityAvailabilityActivity),
            request);

        if (!facilityResult.IsAvailable)
        {
            await context.CallActivityAsync(
                nameof(PublishBookingRejectedActivity),
                new BookingRequestRejectedEvent(
                    BookingRequestId.FromGuid(request.RequestId),
                    BookingRejectionCode.NoCapacityAvailable,
                    "Facility unavailable"));

            return null;
        }

        context.SetCustomStatus("FacilityAvailabilityConfirmed");

        // Step 3: Find available slot
        var slotResult = await context.CallActivityAsync<SlotAllocationResult>(
            nameof(FindAvailableSlotActivity),
            request);

        if (!slotResult.Success)
        {
            await context.CallActivityAsync(
                nameof(PublishBookingRejectedActivity),
                new BookingRequestRejectedEvent(
                    BookingRequestId.FromGuid(request.RequestId),
                    BookingRejectionCode.NoCapacityAvailable,
                    slotResult.Reason ?? "No available slots"));

            return null;
        }

        context.SetCustomStatus("SlotAllocated");

        // Step 4: Record the allocation
        var allocation = await context.CallActivityAsync<AllocationDto>(
            nameof(RecordAllocationActivity),
            slotResult);

        // Step 5: Publish allocation confirmed event
        await context.CallActivityAsync(
            nameof(PublishAllocationConfirmedActivity),
            new SlotAllocationConfirmedEvent(
                SlotAllocationId.FromGuid(allocation.AllocationId),
                BookingRequestId.FromGuid(request.RequestId),
                ParkingSlotId.FromString(slotResult.SlotId.ToString())));

        context.SetCustomStatus("WaitingForArrival");

        // Step 6: Wait for arrival or timeout
        try
        {
            var timeout = request.PlannedArrivalTime.AddMinutes(30) - DateTime.UtcNow;
            var arrival = await context.WaitForExternalEventAsync<ArrivalConfirmationDto>(
                "DriverArrivalConfirmed",
                timeout > TimeSpan.Zero ? timeout : TimeSpan.Zero);

            await context.CallActivityAsync(
                nameof(UpdateAllocationArrivalActivity),
                new UpdateAllocationInfo
                {
                    AllocationId = allocation.AllocationId,
                    ArrivalTime = arrival.ArrivalTime,
                    ConfirmedBy = arrival.ConfirmedBy,
                });

            context.SetCustomStatus("ArrivalConfirmed");
        }
        catch (TaskCanceledException)
        {
            // Arrival window expired — publish expiry event (activity to be added in Phase 1)
            context.SetCustomStatus("AllocationExpired");
            return null;
        }

        // Step 7: Listen for cancellation
        var cancellationTask = context.WaitForExternalEventAsync<ReservationCancellationDto>(
            "ReservationCancelled");

        if (await Task.WhenAny(cancellationTask, Task.Delay(Timeout.Infinite)) == cancellationTask)
        {
            var cancellation = cancellationTask.Result;

            await context.CallActivityAsync(
                nameof(CancelReservationActivity),
                new CancellationInfo
                {
                    AllocationId = allocation.AllocationId,
                    Reason = cancellation.CancellationReason,
                    CancelledBy = cancellation.CancelledBy,
                    CancellationTime = cancellation.CancellationTime,
                });

            context.SetCustomStatus("AllocationCancelled");
            return null;
        }

        return new AllocatedSlotDto
        {
            AllocationId = allocation.AllocationId,
            SlotId = allocation.SlotId,
            FacilityId = request.FacilityId,
            StartTime = request.PlannedArrivalTime,
            EndTime = request.PlannedDepartureTime,
        };
    }
}
