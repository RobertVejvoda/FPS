using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Application.Workflows.Activities;

// Shared helper — appends a lifecycle step to the persisted DrawAttemptDto.
// Uses ETag-based optimistic concurrency with retry to safely handle concurrent
// lifecycle step appends. Activities use read-modify-write because Dapr Workflow
// does not re-execute already-completed activities on replay.
internal static class ActivityLifecycleHelper
{
    private const int MaxRetries = 3;

    internal static async Task AppendStepAsync(
        IDrawRepository repo,
        string drawKey,
        string stepName,
        string stepStatus,
        string? summary = null,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        // Retry logic for ETag conflicts — allows safe concurrent lifecycle updates
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            var drawAttempt = await repo.GetByKeyAsync(drawKey, ct);
            if (drawAttempt is null) return;

            drawAttempt.LifecycleSteps ??= [];
            drawAttempt.LifecycleSteps.Add(new DrawLifecycleStepRecord
            {
                StepName = stepName,
                Status = stepStatus,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Summary = summary,
                ErrorMessage = errorMessage,
            });

            // TrySaveAsync returns true if saved successfully, false on ETag mismatch
            if (await repo.TrySaveAsync(drawAttempt, ct))
            {
                return; // Success
            }

            // ETag conflict — retry with fresh read
            if (attempt < MaxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), ct);
            }
        }

        // Final fallback: use SaveAsync which throws on ETag conflict
        // This ensures visibility of persistent concurrency issues
        var finalAttempt = await repo.GetByKeyAsync(drawKey, ct);
        if (finalAttempt is null) return;

        finalAttempt.LifecycleSteps ??= [];
        finalAttempt.LifecycleSteps.Add(new DrawLifecycleStepRecord
        {
            StepName = stepName,
            Status = stepStatus,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Summary = summary,
            ErrorMessage = errorMessage,
        });

        await repo.SaveAsync(finalAttempt, ct);
    }
}
