using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Workflows;
using FPS.Booking.Domain.ValueObjects;
using MediatR;

namespace FPS.Booking.Application.Commands;

public sealed class TriggerDrawHandler(
    IDrawRepository drawRepository,
    IDrawWorkflowStarter workflowStarter)
    : IRequestHandler<TriggerDrawCommand, TriggerDrawResult>
{
    public async Task<TriggerDrawResult> Handle(TriggerDrawCommand cmd, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(cmd.TimeSlotStart, cmd.TimeSlotEnd);
        var drawKey = DrawKey.Create(cmd.TenantId, cmd.LocationId, cmd.Date, timeSlot);
        var storeKey = drawKey.ToStoreKey();

        var existing = await drawRepository.GetByKeyAsync(storeKey, cancellationToken);

        // Idempotency: already completed — return cached result without re-running.
        if (existing?.Status == "Completed")
        {
            return new TriggerDrawResult(
                storeKey, "Completed",
                existing.AllocatedCount, existing.RejectedCount, existing.WaitlistedCount,
                WasAlreadyCompleted: true);
        }

        // Already running — return in-progress without starting a duplicate.
        if (existing?.Status == "InProgress")
        {
            return new TriggerDrawResult(storeKey, "InProgress", 0, 0, 0, WasAlreadyCompleted: false);
        }

        // New or previously failed draw — start the workflow.
        await workflowStarter.StartAsync(cmd, cancellationToken);
        return new TriggerDrawResult(storeKey, "InProgress", 0, 0, 0, WasAlreadyCompleted: false);
    }
}
