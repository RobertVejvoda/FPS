using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Events;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record AcquireDrawAttemptInput(
    string DrawKey,
    string TenantId,
    string LocationId,
    string Date,
    long Seed,
    string TriggerSource,
    string TriggeredBy);

public sealed record AcquireDrawAttemptOutput(bool WasAlreadyRunning, string StartedAt);

// Creates or acquires the Draw attempt. An existing InProgress attempt means the
// workflow was started by a duplicate trigger; Completed/Failed are handled by
// the workflow before invoking this activity.
public sealed class AcquireDrawAttemptActivity(
    IDrawRepository drawRepository,
    IBookingEventPublisher eventPublisher)
    : WorkflowActivity<AcquireDrawAttemptInput, AcquireDrawAttemptOutput>
{
    public override async Task<AcquireDrawAttemptOutput> RunAsync(
        WorkflowActivityContext context, AcquireDrawAttemptInput input)
    {
        var existing = await drawRepository.GetByKeyAsync(input.DrawKey);
        if (existing?.Status == "InProgress")
            return new AcquireDrawAttemptOutput(WasAlreadyRunning: true, existing.StartedAt.ToString("O"));

        var startedAt = DateTime.UtcNow;
        var attempt = new DrawAttemptDto
        {
            DrawKey = input.DrawKey,
            TenantId = input.TenantId,
            LocationId = input.LocationId,
            Date = DateOnly.Parse(input.Date),
            Status = "InProgress",
            Seed = input.Seed,
            StartedAt = startedAt,
            LifecycleSteps =
            [
                new DrawLifecycleStepRecord
                {
                    StepName = "Scheduled",
                    Status = "Completed",
                    StartedAt = startedAt,
                    CompletedAt = startedAt,
                    Summary = $"Draw triggered by {input.TriggerSource} ({input.TriggeredBy})",
                }
            ],
        };
        await drawRepository.SaveAsync(attempt);

        var publisher = eventPublisher.WithContext(
            new BookingPublishContext(input.TenantId, Guid.NewGuid().ToString(), "system", null));
        _ = publisher.PublishAsync(new DrawAttemptStartedEvent(
            Domain.ValueObjects.DrawKey.Create(
                input.TenantId,
                input.LocationId,
                DateOnly.Parse(input.Date),
                Domain.ValueObjects.TimeSlot.Create(startedAt, startedAt)),
            input.Seed,
            startedAt));

        return new AcquireDrawAttemptOutput(WasAlreadyRunning: false, startedAt.ToString("O"));
    }
}
