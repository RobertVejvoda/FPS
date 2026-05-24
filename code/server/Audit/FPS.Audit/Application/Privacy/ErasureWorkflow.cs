using Dapr.Workflow;

namespace FPS.Audit.Application.Privacy;

public sealed class ErasureWorkflow : Workflow<ErasureWorkflowInput, ErasureWorkflowOutput>
{
    public override async Task<ErasureWorkflowOutput> RunAsync(WorkflowContext context, ErasureWorkflowInput input)
    {
        var svcInput = new ServiceErasureInput(
            input.ErasureRequestId, input.TenantId, input.TargetActorHash, input.TargetUserId);

        // Step 1: Check for active bookings (blocking dependency)
        var bookingCheck = await context.CallActivityAsync<ErasureServiceResult>(
            nameof(CheckActiveBookingsActivity), svcInput);

        await context.CallActivityAsync(nameof(RecordErasureStepActivity),
            new ErasureStepAuditInput(input, bookingCheck));

        if (bookingCheck.Treatment == ErasureTreatment.Blocked)
        {
            return new ErasureWorkflowOutput(
                ErasureStatus.Blocked,
                [bookingCheck],
                bookingCheck.Note);
        }

        // Steps 2–6: Service-owned erasure activities (idempotent, ordered)
        var results = new List<ErasureServiceResult> { bookingCheck };

        foreach (var (activityName, step) in new[]
        {
            (nameof(EraseProfileActivity),       "profile"),
            (nameof(EraseBookingDataActivity),   "booking"),
            (nameof(EraseNotificationActivity),  "notification"),
            (nameof(AnonymiseReportingActivity), "reporting"),
            (nameof(ErasePiiMappingActivity),    "audit-pii"),
        })
        {
            var result = await context.CallActivityAsync<ErasureServiceResult>(activityName, svcInput);
            results.Add(result);
            await context.CallActivityAsync(nameof(RecordErasureStepActivity),
                new ErasureStepAuditInput(input, result));
        }

        var anyFailed = results.Any(r => r.Treatment == ErasureTreatment.Failed);
        var status = anyFailed ? ErasureStatus.PartiallyCompleted : ErasureStatus.Completed;

        return new ErasureWorkflowOutput(status, results, null);
    }
}
