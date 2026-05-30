using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record CompleteDrawAttemptInput(
    string DrawKey,
    string TenantId,
    string LocationId,
    string Date,
    long Seed,
    string AlgorithmVersion,
    string StartedAt,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    List<DrawDecisionDto> Decisions,
    List<string> Tier2CandidateSequence);

public sealed class CompleteDrawAttemptActivity(IDrawRepository drawRepository)
    : WorkflowActivity<CompleteDrawAttemptInput, bool>
{
    public override async Task<bool> RunAsync(
        WorkflowActivityContext context, CompleteDrawAttemptInput input)
    {
        var existing = await drawRepository.GetByKeyAsync(input.DrawKey);
        var steps = existing?.LifecycleSteps ?? [];
        var completedAt = DateTime.UtcNow;
        steps.Add(new DrawLifecycleStepRecord
        {
            StepName = "Completed",
            Status = "Completed",
            StartedAt = completedAt,
            CompletedAt = completedAt,
            Summary = $"{input.AllocatedCount} allocated, {input.RejectedCount} rejected, {input.WaitlistedCount} waitlisted",
        });

        var attempt = new DrawAttemptDto
        {
            DrawKey = input.DrawKey,
            TenantId = input.TenantId,
            LocationId = input.LocationId,
            Date = DateOnly.Parse(input.Date),
            Status = "Completed",
            Seed = input.Seed,
            AlgorithmVersion = input.AlgorithmVersion,
            AllocatedCount = input.AllocatedCount,
            RejectedCount = input.RejectedCount,
            WaitlistedCount = input.WaitlistedCount,
            StartedAt = DateTime.Parse(input.StartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            CompletedAt = completedAt,
            Decisions = input.Decisions,
            Tier2CandidateSequence = input.Tier2CandidateSequence,
            LifecycleSteps = steps,
        };
        await drawRepository.SaveAsync(attempt);
        return true;
    }
}
