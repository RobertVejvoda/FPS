using Dapr.Workflow;
using FPS.Booking.Application.Workflows.Activities;

namespace FPS.Booking.Application.Workflows;

public sealed class DrawWorkflow : Workflow<DrawWorkflowInput, DrawWorkflowOutput>
{
    public override async Task<DrawWorkflowOutput> RunAsync(
        WorkflowContext context, DrawWorkflowInput input)
    {
        // Step 1: Resolve draw key, seed, and effective policy.
        var resolved = await context.CallActivityAsync<ResolvedDrawInput>(
            nameof(ResolveDrawInputActivity), input);

        var drawCtx = new DrawAttemptContext(
            resolved.DrawKey, input.TenantId, input.LocationId,
            input.Date, resolved.Seed, context.CurrentUtcDateTime.ToString("O"));

        // Step 2: Acquire or create the draw attempt (idempotent).
        var acquired = await context.CallActivityAsync<AcquireDrawAttemptOutput>(
            nameof(AcquireDrawAttemptActivity),
            new AcquireDrawAttemptInput(
                resolved.DrawKey, input.TenantId, input.LocationId,
                input.Date, input.TimeSlotStart, input.TimeSlotEnd,
                resolved.Seed, input.TriggerSource, input.TriggeredBy));

        try
        {
            // Step 3: Close the request window.
            await context.CallActivityAsync<bool>(nameof(CloseRequestWindowActivity), drawCtx);

            // Steps 4–6: Load draw inputs in parallel.
            var pendingTask = context.CallActivityAsync<PendingRequestsResult>(
                nameof(LoadPendingRequestsActivity),
                new LoadPendingRequestsInput(resolved.DrawKey, input.TenantId, input.LocationId, input.Date));

            var capacityTask = context.CallActivityAsync<CapacityResult>(
                nameof(LoadCapacityActivity),
                new LoadCapacityInput(resolved.DrawKey, input.TenantId, input.LocationId,
                    input.Date, input.TimeSlotStart, input.TimeSlotEnd));

            var pending = await pendingTask;
            var capacity = await capacityTask;

            var requestorIds = pending.Requests.Select(r => r.RequestedBy).Distinct().ToList();
            var metrics = await context.CallActivityAsync<MetricsResult>(
                nameof(LoadMetricsActivity),
                new LoadMetricsInput(resolved.DrawKey, input.TenantId, requestorIds,
                    input.Date, resolved.AllocationLookbackDays));

            // Step 7: Run the allocation algorithm.
            var allocation = await context.CallActivityAsync<AllocationResult>(
                nameof(RunAllocationActivity),
                new RunAllocationInput(
                    resolved.DrawKey, resolved.Seed,
                    input.TimeSlotStart, input.TimeSlotEnd,
                    pending.Requests, capacity.Slots, metrics.Metrics));

            // Step 8: Persist booking decisions and update metrics.
            await context.CallActivityAsync<bool>(
                nameof(PersistDecisionsActivity),
                new PersistDecisionsInput(
                    resolved.DrawKey, input.TenantId, input.Date,
                    allocation.Decisions, pending.Requests));

            // Step 9: Publish integration events.
            await context.CallActivityAsync<bool>(
                nameof(QueueIntegrationEventsActivity),
                new QueueIntegrationEventsInput(
                    resolved.DrawKey, input.TenantId, input.LocationId, input.Date,
                    resolved.Seed, input.TimeSlotStart, input.TimeSlotEnd,
                    allocation.AllocatedCount, allocation.RejectedCount, allocation.WaitlistedCount,
                    allocation.Decisions, pending.Requests));

            // Step 10: Finalize the draw attempt.
            await context.CallActivityAsync<bool>(
                nameof(CompleteDrawAttemptActivity),
                new CompleteDrawAttemptInput(
                    resolved.DrawKey, input.TenantId, input.LocationId, input.Date,
                    resolved.Seed, allocation.AlgorithmVersion, acquired.StartedAt,
                    allocation.AllocatedCount, allocation.RejectedCount, allocation.WaitlistedCount,
                    allocation.Decisions, allocation.Tier2CandidateSequence));

            return new DrawWorkflowOutput(
                resolved.DrawKey, "Completed",
                allocation.AllocatedCount, allocation.RejectedCount, allocation.WaitlistedCount,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            // Step 11: Record failure — safe error message only, no stack trace.
            var safeMessage = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message;
            await context.CallActivityAsync<bool>(
                nameof(FailDrawAttemptActivity),
                new FailDrawAttemptInput(
                    resolved.DrawKey, input.TenantId, input.LocationId,
                    input.Date, resolved.Seed, acquired.StartedAt, safeMessage));

            return new DrawWorkflowOutput(
                resolved.DrawKey, "Failed", 0, 0, 0, safeMessage);
        }
    }
}
