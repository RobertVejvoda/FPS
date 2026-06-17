using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record FailDrawAttemptInput(
    string DrawKey,
    string TenantId,
    string LocationId,
    string Date,
    long Seed,
    string StartedAt,
    string SafeErrorMessage,
    string? TimeSlotStart = null,
    string? TimeSlotEnd = null,
    string? TriggerSource = null,
    string? TriggeredBy = null,
    string? Reason = null,
    // Internal diagnostic detail — logged for technical troubleshooting only; never stored in state or published.
    string? DiagnosticMessage = null);

public sealed class FailDrawAttemptActivity(
    IDrawRepository drawRepository,
    IBookingEventPublisher eventPublisher,
    ILogger<FailDrawAttemptActivity> logger)
    : WorkflowActivity<FailDrawAttemptInput, bool>
{
    public override async Task<bool> RunAsync(
        WorkflowActivityContext context, FailDrawAttemptInput input)
    {
        // Log the diagnostic detail for technical troubleshooting.
        // DiagnosticMessage contains the raw exception text and is never published to DataHub.
        var diagnostic = input.DiagnosticMessage ?? input.SafeErrorMessage;
        logger.LogWarning(
            "Draw workflow failed for {DrawKey}. Diagnostic: {DiagnosticMessage}",
            input.DrawKey, diagnostic);

        var existing = await drawRepository.GetByKeyAsync(input.DrawKey);
        var steps = existing?.LifecycleSteps ?? [];
        var failedAt = DateTime.UtcNow;
        steps.Add(new DrawLifecycleStepRecord
        {
            StepName = "DrawFailed",
            Status = "Failed",
            StartedAt = failedAt,
            CompletedAt = failedAt,
            // Store only the safe message — ErrorMessage is surfaced by GetDrawLifecycleHandler
            // to hr_manager/admin/auditor callers and must not contain raw exception details.
            ErrorMessage = input.SafeErrorMessage,
        });

        var attempt = new DrawAttemptDto
        {
            DrawKey = input.DrawKey,
            TenantId = input.TenantId,
            LocationId = input.LocationId,
            Date = DateOnly.TryParse(input.Date, out var attemptDate) ? attemptDate : DateOnly.FromDateTime(failedAt),
            Status = "Failed",
            Seed = input.Seed,
            StartedAt = DateTime.TryParse(input.StartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var attemptStarted)
                ? attemptStarted : failedAt,
            CompletedAt = failedAt,
            Decisions = existing?.Decisions ?? [],
            LifecycleSteps = steps,
        };
        await drawRepository.SaveAsync(attempt);

        // DRAW009: publish drawFailed event so DataHub can project the failure status
        // and safe failure reason without requiring a direct query back to Booking.
        if (!DateOnly.TryParse(input.Date, out var date))
            date = DateOnly.FromDateTime(failedAt);

        if (!DateTime.TryParse(input.StartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
            startedAt = failedAt;

        var slotStart = DateTime.TryParse(input.TimeSlotStart, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts)
            ? ts : failedAt.Date;
        var slotEnd = DateTime.TryParse(input.TimeSlotEnd, null, System.Globalization.DateTimeStyles.RoundtripKind, out var te)
            ? te : failedAt.Date.AddHours(1);
        var drawKey = DrawKey.Create(input.TenantId, input.LocationId, date,
            TimeSlot.Create(slotStart, slotEnd));

        var safeSteps = steps
            .Select(s => new DrawProgressStepRecord(s.StepName, s.Status, s.Summary, s.StartedAt))
            .ToList();

        var operatorTriggered = input.TriggerSource != "scheduled";
        var actorType = operatorTriggered ? "hr_manager" : "system";
        var actorId = operatorTriggered ? input.TriggeredBy : null;
        var publisher = eventPublisher.WithContext(
            new BookingPublishContext(input.TenantId, Guid.NewGuid().ToString(), actorType, actorId));
        await publisher.PublishAsync(new DrawAttemptFailedEvent(
            DrawKey: drawKey,
            Seed: input.Seed,
            SafeFailureReason: input.SafeErrorMessage,
            FailedAt: failedAt,
            DrawAttemptId: input.DrawKey,
            TriggerSource: input.TriggerSource,
            RunReason: input.Reason,
            TriggeredBy: input.TriggeredBy,
            LifecycleSteps: safeSteps));

        return true;
    }
}
