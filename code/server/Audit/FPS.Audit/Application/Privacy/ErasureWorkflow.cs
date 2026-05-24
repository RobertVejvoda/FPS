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

        if (bookingCheck.Treatment == ErasureTreatment.Blocked)
        {
            return new ErasureWorkflowOutput(
                ErasureStatus.Blocked,
                [bookingCheck],
                bookingCheck.Note);
        }

        // Steps 2–6: Service-owned erasure activities (run in order; each is idempotent)
        var results = new List<ErasureServiceResult> { bookingCheck };

        results.Add(await context.CallActivityAsync<ErasureServiceResult>(
            nameof(EraseProfileActivity), svcInput));

        results.Add(await context.CallActivityAsync<ErasureServiceResult>(
            nameof(EraseBookingDataActivity), svcInput));

        results.Add(await context.CallActivityAsync<ErasureServiceResult>(
            nameof(EraseNotificationActivity), svcInput));

        results.Add(await context.CallActivityAsync<ErasureServiceResult>(
            nameof(AnonymiseReportingActivity), svcInput));

        // Step 7: Delete PII mapping so audit records no longer resolve to a person
        results.Add(await context.CallActivityAsync<ErasureServiceResult>(
            nameof(ErasePiiMappingActivity), svcInput));

        var anyFailed = results.Any(r => r.Treatment == ErasureTreatment.Failed);
        var status = anyFailed ? ErasureStatus.PartiallyCompleted : ErasureStatus.Completed;

        return new ErasureWorkflowOutput(status, results, null);
    }
}
