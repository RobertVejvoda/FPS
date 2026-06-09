using Dapr.Workflow;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;

namespace FPS.Booking.Application.Workflows.Activities;

public sealed record LoadMetricsInput(
    string DrawKey,
    string TenantId,
    List<string> RequestorIds,
    string Date,
    int LookbackDays);

public sealed class LoadMetricsActivity(
    IEmployeeMetricsService metricsService,
    IDrawRepository drawRepository)
    : WorkflowActivity<LoadMetricsInput, MetricsResult>
{
    public override async Task<MetricsResult> RunAsync(
        WorkflowActivityContext context, LoadMetricsInput input)
    {
        var date = DateOnly.Parse(input.Date);
        var snapshot = await metricsService.GetMetricsSnapshotAsync(
            input.TenantId, input.RequestorIds, date, input.LookbackDays);

        var metrics = snapshot.Values
            .Select(m => new EmployeeMetricsData(m.RequestorId, m.RecentAllocationCount, m.ActivePenaltyScore))
            .ToList();

        await ActivityLifecycleHelper.AppendStepAsync(
            drawRepository, input.DrawKey,
            "MetricsLoaded", "Completed",
            summary: $"{metrics.Count} requestor metric record(s)");

        return new MetricsResult(metrics);
    }
}
