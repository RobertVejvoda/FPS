using Dapr.Workflow;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record LoadCapacityInput(
    string DrawKey,
    string TenantId,
    string LocationId,
    string Date,
    string TimeSlotStart,
    string TimeSlotEnd);

public sealed class LoadCapacityActivity(
    IAvailableSlotService slotService,
    IDrawRepository drawRepository)
    : WorkflowActivity<LoadCapacityInput, CapacityResult>
{
    public override async Task<CapacityResult> RunAsync(
        WorkflowActivityContext context, LoadCapacityInput input)
    {
        var date = DateOnly.Parse(input.Date);
        var slotStart = DateTime.Parse(input.TimeSlotStart, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var slotEnd = DateTime.Parse(input.TimeSlotEnd, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var timeSlot = TimeSlot.Create(slotStart, slotEnd);

        var slots = await slotService.GetAvailableSlotsAsync(
            input.TenantId, input.LocationId, date, timeSlot);

        var slotData = slots.Select(s => new SlotData(
            s.SlotId.Value, s.HasCharger, s.IsAccessible, s.IsCompanyCarReserved, s.IsMotorcycleCapacity)).ToList();

        await ActivityLifecycleHelper.AppendStepAsync(
            drawRepository, input.DrawKey,
            "CapacityLoaded", "Completed",
            summary: $"{slotData.Count} available slot(s)");

        return new CapacityResult(slotData);
    }
}
