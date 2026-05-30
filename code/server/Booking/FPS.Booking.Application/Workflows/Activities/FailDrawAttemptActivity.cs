using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record FailDrawAttemptInput(
    string DrawKey,
    string TenantId,
    string LocationId,
    string Date,
    long Seed,
    string StartedAt,
    string SafeErrorMessage);

public sealed class FailDrawAttemptActivity(IDrawRepository drawRepository)
    : WorkflowActivity<FailDrawAttemptInput, bool>
{
    public override async Task<bool> RunAsync(
        WorkflowActivityContext context, FailDrawAttemptInput input)
    {
        var existing = await drawRepository.GetByKeyAsync(input.DrawKey);
        var steps = existing?.LifecycleSteps ?? [];
        var failedAt = DateTime.UtcNow;
        steps.Add(new DrawLifecycleStepRecord
        {
            StepName = "DrawFailed",
            Status = "Failed",
            StartedAt = failedAt,
            CompletedAt = failedAt,
            ErrorMessage = input.SafeErrorMessage,
        });

        var attempt = new DrawAttemptDto
        {
            DrawKey = input.DrawKey,
            TenantId = input.TenantId,
            LocationId = input.LocationId,
            Date = DateOnly.Parse(input.Date),
            Status = "Failed",
            Seed = input.Seed,
            StartedAt = DateTime.Parse(input.StartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            CompletedAt = failedAt,
            Decisions = existing?.Decisions ?? [],
            LifecycleSteps = steps,
        };
        await drawRepository.SaveAsync(attempt);
        return true;
    }
}
