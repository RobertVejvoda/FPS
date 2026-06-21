using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Services;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record RunAllocationInput(
    string DrawKey,
    long Seed,
    string TimeSlotStart,
    string TimeSlotEnd,
    List<BookingRequestDto> PendingRequests,
    List<SlotData> AvailableSlots,
    List<EmployeeMetricsData> Metrics);

public sealed class RunAllocationActivity(
    DrawService drawService,
    IDrawRepository drawRepository)
    : WorkflowActivity<RunAllocationInput, AllocationResult>
{
    public override async Task<AllocationResult> RunAsync(
        WorkflowActivityContext context, RunAllocationInput input)
    {
        var slotStart = DateTime.Parse(input.TimeSlotStart, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var slotEnd = DateTime.Parse(input.TimeSlotEnd, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var timeSlot = TimeSlot.Create(slotStart, slotEnd);

        var requests = input.PendingRequests.Select(d => BookingRequest.Restore(
            BookingRequestId.FromGuid(d.RequestId),
            UserId.FromString(d.RequestedBy),
            VehicleInformation.Create(
                "UNKNOWN",
                // Restore the persisted VehicleType so motorcycles match motorcycle-only
                // capacity instead of being treated as Sedan. Older dtos without a
                // VehicleType default to Sedan for backward compatibility.
                Enum.TryParse<VehicleType>(d.VehicleType, ignoreCase: true, out var vt)
                    ? vt
                    : VehicleType.Sedan,
                d.VehicleIsElectric,
                d.RequiresAccessibleSpot,
                d.VehicleIsCompanyCar),
            TimeSlot.Create(d.PlannedArrivalTime, d.PlannedDepartureTime),
            BookingRequestStatus.Pending,
            d.RequestedAt)).ToList();

        var slots = input.AvailableSlots.Select(s => AvailableSlot.Create(
            ParkingSlotId.FromString(s.SlotId),
            s.IsActive,
            s.HasCharger,
            s.IsAccessible,
            s.IsCompanyCarReserved,
            s.ReservedForUserId,
            s.IsMotorcycleCapacity)).ToList();

        var metricsMap = input.Metrics.ToDictionary(
            m => m.RequestorId,
            m => new EmployeeMetrics(m.RequestorId, m.RecentAllocationCount, m.ActivePenaltyScore));

        var drawResult = drawService.RunDraw(requests, slots, metricsMap, input.Seed);

        var decisions = drawResult.Decisions.Select(d => new DrawDecisionDto
        {
            RequestId = d.RequestId.Value.ToString(),
            RequestorId = d.RequestorId.Value.ToString(),
            Outcome = d.Outcome.ToString(),
            SlotId = d.SlotId?.Value,
            Reason = d.Reason,
            AllocatedSlotReference = d.Outcome == DrawOutcome.Allocated && d.SlotId != null
                ? $"Slot-{d.SlotId.Value}"
                : null,
            IsTier1Guaranteed = d.IsTier1Guaranteed,
        }).ToList();

        var tier2 = drawResult.Tier2CandidateSequence.Select(id => id.Value.ToString()).ToList();

        var allocated = decisions.Count(d => d.Outcome == DrawOutcome.Allocated.ToString());
        var rejected = decisions.Count(d => d.Outcome == DrawOutcome.Rejected.ToString());
        var waitlisted = decisions.Count(d => d.Outcome == DrawOutcome.Waitlisted.ToString());

        await ActivityLifecycleHelper.AppendStepAsync(
            drawRepository, input.DrawKey,
            "WeightedAllocationCompleted", "Completed",
            summary: $"{allocated} allocated, {rejected} rejected, {waitlisted} waitlisted; algorithm: {drawResult.AlgorithmVersion}");

        return new AllocationResult(decisions, tier2, drawResult.AlgorithmVersion, allocated, rejected, waitlisted);
    }
}
