using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Application.Workflows.Activities;

// Shared helper — appends a lifecycle step to the persisted DrawAttemptDto.
// Activities use read-modify-write because Dapr Workflow does not re-execute
// already-completed activities on replay, so duplicate steps are not a concern.
internal static class ActivityLifecycleHelper
{
    internal static async Task AppendStepAsync(
        IDrawRepository repo,
        string drawKey,
        string stepName,
        string stepStatus,
        string? summary = null,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        var attempt = await repo.GetByKeyAsync(drawKey, ct);
        if (attempt is null) return;
        attempt.LifecycleSteps ??= [];
        attempt.LifecycleSteps.Add(new DrawLifecycleStepRecord
        {
            StepName = stepName,
            Status = stepStatus,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Summary = summary,
            ErrorMessage = errorMessage,
        });
        await repo.SaveAsync(attempt, ct);
    }
}
