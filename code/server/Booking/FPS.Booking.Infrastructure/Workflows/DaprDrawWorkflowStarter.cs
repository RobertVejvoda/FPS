using Dapr.Workflow;
using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Workflows;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Infrastructure.Workflows;

public sealed class DaprDrawWorkflowStarter(DaprWorkflowClient workflowClient) : IDrawWorkflowStarter
{
    public async Task<DrawStartResult> StartAsync(TriggerDrawCommand command, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(command.TimeSlotStart, command.TimeSlotEnd);
        var drawKey = DrawKey.Create(command.TenantId, command.LocationId, command.Date, timeSlot);
        var storeKey = drawKey.ToStoreKey();

        // Instance ID is deterministic from the draw key so duplicate triggers are safe.
        var instanceId = storeKey;

        var workflowInput = new DrawWorkflowInput(
            command.TenantId,
            command.LocationId,
            command.Date.ToString("yyyy-MM-dd"),
            command.TimeSlotStart.ToString("O"),
            command.TimeSlotEnd.ToString("O"),
            command.Reason,
            command.TriggerSource,
            command.TriggeredBy);

        try
        {
            await workflowClient.ScheduleNewWorkflowAsync(
                nameof(DrawWorkflow), instanceId, workflowInput);
            return new DrawStartResult(storeKey, instanceId, "Started");
        }
        catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase))
        {
            return new DrawStartResult(storeKey, instanceId, "AlreadyRunning");
        }
    }
}
