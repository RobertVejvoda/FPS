using Dapr.Workflow;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record LoadPendingRequestsInput(
    string DrawKey,
    string TenantId,
    string LocationId,
    string Date);

public sealed class LoadPendingRequestsActivity(
    IBookingQueryRepository bookingQueryRepository,
    IDrawRepository drawRepository)
    : WorkflowActivity<LoadPendingRequestsInput, PendingRequestsResult>
{
    public override async Task<PendingRequestsResult> RunAsync(
        WorkflowActivityContext context, LoadPendingRequestsInput input)
    {
        var date = DateOnly.Parse(input.Date);
        var requests = await bookingQueryRepository.GetPendingRequestsForDrawAsync(
            input.TenantId, input.LocationId, date);

        await ActivityLifecycleHelper.AppendStepAsync(
            drawRepository, input.DrawKey,
            "RequestsLoaded", "Completed",
            summary: $"{requests.Count} pending request(s)");

        return new PendingRequestsResult(requests.ToList());
    }
}
