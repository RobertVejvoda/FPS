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
        // Completed draws must not be mutated.
        if (existing?.Status == "Completed")
        {
            return new TriggerDrawResult(
                storeKey, "Completed",
                existing.AllocatedCount, existing.RejectedCount, existing.WaitlistedCount,
                WasAlreadyCompleted: true);
        }

        // Already running — return in-progress without starting a duplicate.
        if (existing?.Status == "InProgress")
            return new TriggerDrawResult(storeKey, "InProgress", 0, 0, 0, WasAlreadyCompleted: false);

        // Previously failed — allow explicit recovery if requested
        if (existing?.Status == "Failed")
        {
            if (!cmd.AllowRecovery)
            {
                // Surface failed state without automatic retry
                return new TriggerDrawResult(storeKey, "Failed", 0, 0, 0, WasAlreadyCompleted: false);
            }

            // Recovery mode: archive the failed attempt and start fresh
            existing.Status = "FailedArchived";
            existing.LifecycleSteps ??= [];
            existing.LifecycleSteps.Add(new DrawLifecycleStepRecord
            {
                StepName = "RecoveryInitiated",
                Status = "Completed",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Summary = $"Recovery triggered: {cmd.Reason}. Previous failure archived.",
            });
            await drawRepository.SaveAsync(existing, cancellationToken);

            // Start new workflow with recovery trigger source
            var recoveryCmd = cmd with { TriggerSource = "recovery" };
            await workflowStarter.StartAsync(recoveryCmd, cancellationToken);
            return new TriggerDrawResult(storeKey, "InProgress", 0, 0, 0, WasAlreadyCompleted: false);
        }

        // No prior attempt — start the workflow.
        await workflowStarter.StartAsync(cmd, cancellationToken);
        return new TriggerDrawResult(storeKey, "InProgress", 0, 0, 0, WasAlreadyCompleted: false);
    }
}
