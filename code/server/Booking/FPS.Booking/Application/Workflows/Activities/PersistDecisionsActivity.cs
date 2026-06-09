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

        foreach (var decision in input.Decisions)
        {
            if (!Guid.TryParse(decision.RequestId, out var requestGuid)) continue;

            // Read current status to guard against duplicate application on activity retry/replay.
            // Only requests still in Pending state should be updated; those already decided are skipped.
            var current = await bookingRepository.GetBookingRequestAsync(input.TenantId, requestGuid);
            if (current is null || current.Status != "Pending") continue;

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
