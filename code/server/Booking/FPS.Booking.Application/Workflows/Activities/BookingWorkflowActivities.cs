using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.ValueObjects;
using FPS.SharedKernel.DomainEvents;

namespace FPS.Booking.Application.Workflows.Activities;

public class RecordBookingRequestActivity : WorkflowActivity<BookingRequestDto, bool>
{
    private readonly IBookingRepository _repository;
    public RecordBookingRequestActivity(IBookingRepository repository) => _repository = repository;

    public override async Task<bool> RunAsync(WorkflowActivityContext context, BookingRequestDto input)
    {
        await _repository.CreateBookingRequestAsync(input);
        return true;
    }
}

public class CheckFacilityAvailabilityActivity : WorkflowActivity<BookingRequestDto, FacilityAvailabilityResult>
{
    private readonly IAllocationService _allocationService;
    public CheckFacilityAvailabilityActivity(IAllocationService allocationService) => _allocationService = allocationService;

    public override Task<FacilityAvailabilityResult> RunAsync(WorkflowActivityContext context, BookingRequestDto input)
        => _allocationService.CheckFacilityAvailabilityAsync(input.FacilityId, input.PlannedArrivalTime, input.PlannedDepartureTime);
}

public class FindAvailableSlotActivity : WorkflowActivity<BookingRequestDto, SlotAllocationResult>
{
    private readonly IAllocationService _allocationService;
    public FindAvailableSlotActivity(IAllocationService allocationService) => _allocationService = allocationService;

    public override Task<SlotAllocationResult> RunAsync(WorkflowActivityContext context, BookingRequestDto input)
        => _allocationService.FindAvailableSlotAsync(input.FacilityId, input.VehicleId, input.PlannedArrivalTime, input.PlannedDepartureTime);
}

public class RecordAllocationActivity : WorkflowActivity<SlotAllocationResult, AllocationDto>
{
    private readonly IBookingRepository _repository;
    public RecordAllocationActivity(IBookingRepository repository) => _repository = repository;

    public override async Task<AllocationDto> RunAsync(WorkflowActivityContext context, SlotAllocationResult input)
    {
        var allocation = new AllocationDto
        {
            AllocationId = Guid.NewGuid(),
            RequestId = input.RequestId,
            SlotId = input.SlotId,
            Status = SlotAllocationStatus.Reserved.ToString(),
            AllocatedAt = DateTime.UtcNow,
        };
        await _repository.CreateAllocationAsync(allocation);
        return allocation;
    }
}

public class PublishBookingRejectedActivity : WorkflowActivity<BookingRequestRejectedEvent, bool>
{
    private readonly IEventPublisher _publisher;
    public PublishBookingRejectedActivity(IEventPublisher publisher) => _publisher = publisher;

    public override async Task<bool> RunAsync(WorkflowActivityContext context, BookingRequestRejectedEvent input)
    {
        await _publisher.PublishAsync(input);
        return true;
    }
}

public class PublishAllocationConfirmedActivity : WorkflowActivity<SlotAllocationConfirmedEvent, bool>
{
    private readonly IEventPublisher _publisher;
    public PublishAllocationConfirmedActivity(IEventPublisher publisher) => _publisher = publisher;

    public override async Task<bool> RunAsync(WorkflowActivityContext context, SlotAllocationConfirmedEvent input)
    {
        await _publisher.PublishAsync(input);
        return true;
    }
}

public class UpdateAllocationArrivalActivity : WorkflowActivity<UpdateAllocationInfo, bool>
{
    private readonly IBookingRepository _repository;
    public UpdateAllocationArrivalActivity(IBookingRepository repository) => _repository = repository;

    public override async Task<bool> RunAsync(WorkflowActivityContext context, UpdateAllocationInfo input)
    {
        await _repository.UpdateAllocationArrivalAsync(input.AllocationId, input.ArrivalTime, input.ConfirmedBy);
        return true;
    }
}

public class CancelReservationActivity : WorkflowActivity<CancellationInfo, bool>
{
    private readonly IBookingRepository _repository;
    public CancelReservationActivity(IBookingRepository repository) => _repository = repository;

    public override async Task<bool> RunAsync(WorkflowActivityContext context, CancellationInfo input)
    {
        await _repository.UpdateAllocationStatusAsync(input.AllocationId, SlotAllocationStatus.Cancelled.ToString(), input.Reason);
        return true;
    }
}
