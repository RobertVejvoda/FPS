using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Events;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record QueueIntegrationEventsInput(
    string DrawKey,
    string TenantId,
    string LocationId,
    string Date,
    long Seed,
    string TimeSlotStart,
    string TimeSlotEnd,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    List<DrawDecisionDto> Decisions,
    List<BookingRequestDto> PendingRequests,
    // #472: HR-supplied metadata mirrored onto the completed event so
    // DataHub's projection still has the trigger context when the
    // started event is missed or rebuilt.
    string? TriggerSource = null,
    string? Reason = null,
    string? TriggeredBy = null);

public sealed class QueueIntegrationEventsActivity(
    IBookingEventPublisher eventPublisher,
    IDrawRepository drawRepository)
    : WorkflowActivity<QueueIntegrationEventsInput, bool>
{
    public override async Task<bool> RunAsync(
        WorkflowActivityContext context, QueueIntegrationEventsInput input)
    {
        var date = DateOnly.Parse(input.Date);
        var slotStart = DateTime.Parse(input.TimeSlotStart, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var slotEnd = DateTime.Parse(input.TimeSlotEnd, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var timeSlot = TimeSlot.Create(slotStart, slotEnd);
        var drawKey = DrawKey.Create(input.TenantId, input.LocationId, date, timeSlot);

        foreach (var decision in input.Decisions)
        {
            var dto = input.PendingRequests.FirstOrDefault(r => r.RequestId.ToString() == decision.RequestId);
            if (dto is null) continue;

            var decisionPublisher = eventPublisher.WithContext(new BookingPublishContext(
                input.TenantId, Guid.NewGuid().ToString(), "system", null,
                SubjectRequestorId: decision.RequestorId,
                AllocationSource: "draw"));

            switch (decision.Outcome)
            {
                case "Allocated" when decision.SlotId is not null:
                    await decisionPublisher.PublishAsync(new SlotAllocationCreatedEvent(
                        SlotAllocationId.New(),
                        BookingRequestId.FromGuid(dto.RequestId),
                        ParkingSlotId.FromString(decision.SlotId),
                        timeSlot));
                    break;

                case "Rejected":
                    await decisionPublisher.PublishAsync(new BookingRequestRejectedEvent(
                        BookingRequestId.FromGuid(dto.RequestId),
                        BookingRejectionCode.DrawNotSelected,
                        decision.Reason ?? "Not selected in draw"));
                    break;
            }
        }

        // #472 / PR #492 review: mirror the actor/trigger context applied
        // on draw start. Any non-scheduled trigger is operator-initiated
        // (manual + recovery) and surfaces the authenticated runner.
        var operatorTriggered = input.TriggerSource != "scheduled";
        var actorType = operatorTriggered ? "hr_manager" : "system";
        var actorId = operatorTriggered ? input.TriggeredBy : null;
        var completedPublisher = eventPublisher.WithContext(
            new BookingPublishContext(input.TenantId, Guid.NewGuid().ToString(), actorType, actorId));

        // DRAW009: read the persisted lifecycle steps before publishing so DataHub
        // can project them as an ordered progress record. Steps are appended by
        // each workflow activity up to this point. We also append the terminal
        // EventsQueued and Completed steps here so DataHub receives a complete
        // lifecycle snapshot (both steps are persisted by subsequent operations).
        var drawAttempt = await drawRepository.GetByKeyAsync(input.DrawKey);
        List<DrawProgressStepRecord>? safeSteps = null;
        if (drawAttempt?.LifecycleSteps is not null)
        {
            var now = DateTime.UtcNow;
            safeSteps = drawAttempt.LifecycleSteps
                .Select(s => new DrawProgressStepRecord(s.StepName, s.Status, s.Summary, s.StartedAt))
                .ToList();
            // Append the terminal steps that will be persisted after publishing.
            // EventsQueued and Completed are sequential; offset by 1 ms so their
            // OccurredAt values are strictly ordered in the DataHub progress view.
            safeSteps.Add(new DrawProgressStepRecord(
                "EventsQueued", "Completed",
                $"DrawAttemptCompleted + {input.Decisions.Count} decision event(s) published", now));
            safeSteps.Add(new DrawProgressStepRecord(
                "Completed", "Completed",
                $"{input.AllocatedCount} allocated, {input.RejectedCount} rejected, {input.WaitlistedCount} waitlisted",
                now.AddMilliseconds(1)));
        }

        await completedPublisher.PublishAsync(new DrawAttemptCompletedEvent(
            drawKey, input.Seed,
            input.AllocatedCount, input.RejectedCount, input.WaitlistedCount,
            DateTime.UtcNow,
            DrawAttemptId: input.DrawKey,
            TriggerSource: input.TriggerSource,
            RunReason: input.Reason,
            TriggeredBy: input.TriggeredBy,
            LifecycleSteps: safeSteps));

        await ActivityLifecycleHelper.AppendStepAsync(
            drawRepository, input.DrawKey,
            "EventsQueued", "Completed",
            summary: $"DrawAttemptCompleted + {input.Decisions.Count} decision event(s) published");

        return true;
    }
}
