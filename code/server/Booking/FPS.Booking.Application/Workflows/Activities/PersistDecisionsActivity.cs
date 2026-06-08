using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record PersistDecisionsInput(
    string DrawKey,
    string TenantId,
    string Date,
    List<DrawDecisionDto> Decisions,
    List<BookingRequestDto> PendingRequests);

public sealed class PersistDecisionsActivity(
    IBookingRepository bookingRepository,
    IEmployeeMetricsService metricsService,
    IDrawRepository drawRepository)
    : WorkflowActivity<PersistDecisionsInput, bool>
{
    public override async Task<bool> RunAsync(
        WorkflowActivityContext context, PersistDecisionsInput input)
    {
        var date = DateOnly.Parse(input.Date);

        // Build lookup to check current request status for idempotency
        var pendingLookup = input.PendingRequests.ToDictionary(r => r.RequestId.ToString());

        foreach (var decision in input.Decisions)
        {
            if (!Guid.TryParse(decision.RequestId, out var requestGuid)) continue;

            // Idempotency check: only update if request is still in expected Pending state
            // Prevents duplicate status changes if activity is retried or replayed
            if (!pendingLookup.ContainsKey(decision.RequestId))
            {
                // Request not in pending list — skip to avoid duplicate update
                continue;
            }

            switch (decision.Outcome)
            {
                case "Allocated":
                    await bookingRepository.UpdateBookingRequestStatusAsync(
                        input.TenantId, requestGuid, "Allocated");
                    await metricsService.IncrementRecentAllocationAsync(
                        input.TenantId, decision.RequestorId, date);
                    break;

                case "Rejected":
                    await bookingRepository.UpdateBookingRequestStatusAsync(
                        input.TenantId, requestGuid, "Rejected", decision.Reason,
                        BookingRejectionCode.DrawNotSelected.ToString());
                    break;

                // Waitlisted: remains Pending, no status update needed
            }
        }

        await ActivityLifecycleHelper.AppendStepAsync(
            drawRepository, input.DrawKey,
            "DecisionsPersisted", "Completed",
            summary: $"{input.Decisions.Count} decision(s) persisted to booking store");

        return true;
    }
}
