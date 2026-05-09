using System;
using System.Threading.Tasks;
using Dapr.Client;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly DaprClient _daprClient;
        
        public BookingController(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }
        
        [HttpPost("submit-request")]
        public async Task<IActionResult> SubmitBookingRequest(SubmitBookingRequestCommand command)
        {
            // Start the booking workflow
            var bookingRequest = new BookingRequestDto
            {
                RequestId = Guid.NewGuid(),
                VehicleId = command.VehicleId,
                FacilityId = command.FacilityId,
                PlannedArrivalTime = command.PlannedArrivalTime,
                PlannedDepartureTime = command.PlannedDepartureTime,
                RequestedBy = command.RequestedBy
            };
            
            // Start the workflow instance with the request ID as the instance ID
            await _daprClient.StartWorkflowAsync(
                "BookingWorkflow",
                bookingRequest.RequestId.ToString(),
                bookingRequest);
                          
            // Return response with tracking info
            return AcceptedAtAction(
                nameof(CheckAllocationStatus), 
                new { requestId = bookingRequest.RequestId }, 
                new { RequestTrackingId = bookingRequest.RequestId });
        }
        
        [HttpGet("allocation-status/{requestId}")]
        public async Task<IActionResult> CheckAllocationStatus(Guid requestId)
        {
            // Get workflow status
            var workflowState = await _daprClient.GetWorkflowAsync(requestId.ToString());
                
            if (workflowState == null)
            {
                return NotFound(new { Message = "Parking request not found" });
            }
            
            // Get custom status from the workflow if available
            var customStatus = workflowState.CustomStatus?.ToString();
            
            return Ok(new AllocationStatusDto
            {
                RequestId = requestId,
                Status = MapWorkflowStatusToDomain(workflowState.RuntimeStatus),
                CurrentPhase = customStatus ?? "Processing",
                LastUpdated = workflowState.LastUpdatedAt,
                AllocatedSlot = workflowState.Output as AllocatedSlotDto
            });
        }
        
        [HttpPost("confirm-arrival/{allocationId}")]
        public async Task<IActionResult> ConfirmArrival(Guid allocationId, ConfirmArrivalCommand command)
        {
            // Raise event to the workflow to confirm the driver has arrived
            await _daprClient.RaiseWorkflowEventAsync(
                allocationId.ToString(),
                "DriverArrivalConfirmed",
                new ArrivalConfirmationDto
                {
                    ArrivalTime = command.ActualArrivalTime,
                    ConfirmedBy = command.ConfirmedBy
                });
                
            return Accepted(new { Message = "Arrival confirmation recorded" });
        }
        
        [HttpPost("cancel-reservation/{requestId}")]
        public async Task<IActionResult> CancelReservation(Guid requestId, CancelReservationCommand command)
        {
            // Check if the request exists
            var workflowState = await _daprClient.GetWorkflowAsync(requestId.ToString());
            
            if (workflowState == null)
            {
                return NotFound(new { Message = "Parking reservation not found" });
            }
            
            // Raise cancellation event
            await _daprClient.RaiseWorkflowEventAsync(
                requestId.ToString(),
                "ReservationCancelled",
                new ReservationCancellationDto
                {
                    CancellationReason = command.Reason,
                    CancelledBy = command.CancelledBy,
                    CancellationTime = DateTime.UtcNow
                });
                
            return Accepted(new { Message = "Cancellation request received" });
        }
        
        [HttpGet("check-availability")]
        public async Task<IActionResult> CheckAvailability([FromQuery] CheckAvailabilityQuery query)
        {
            // Query available slots based on parameters
            var availabilityResult = await _daprClient.InvokeMethodAsync<CheckAvailabilityQuery, AvailabilityResultDto>(
                HttpMethod.Get, 
                "allocation-service", 
                "availability", 
                query);
                
            return Ok(availabilityResult);
        }
        
        private string MapWorkflowStatusToDomain(string workflowStatus)
        {
            return workflowStatus switch
            {
                "Running" => "Processing",
                "Completed" => "Allocated",
                "Failed" => "AllocationFailed",
                "Terminated" => "Cancelled",
                _ => workflowStatus
            };
        }
    }
}