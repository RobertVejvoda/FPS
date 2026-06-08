using FPS.Reporting.Application;
using FPS.Reporting.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Reporting.Controllers;

[ApiController]
[Authorize(Roles = $"{ReportingRoles.HrManager},{ReportingRoles.Admin},{ReportingRoles.ReportViewer}")]
public sealed class ReportingController(ReportingQueryService queryService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/reports/parking/summary")]
    public async Task<IActionResult> GetSummary([FromQuery] ReportingQueryRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await queryService.GetSummaryAsync(request, currentUser.TenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/reports/parking/fairness")]
    public async Task<IActionResult> GetFairness([FromQuery] FairnessQueryRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await queryService.GetFairnessAsync(request, currentUser.TenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/reports/parking/dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] ReportingQueryRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await queryService.GetDashboardAsync(request, currentUser.TenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/reports/parking/summary.csv")]
    public async Task<IActionResult> GetSummaryCsv([FromQuery] ReportingQueryRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var csv = await queryService.GetSummaryCsvAsync(request, currentUser.TenantId, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "fps-parking-summary.csv");
    }

    [HttpGet("/reports/parking/utilization")]
    public async Task<IActionResult> GetUtilization([FromQuery] ReportingQueryRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await queryService.GetUtilizationAsync(request, currentUser.TenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/reports/parking/reason-codes")]
    public async Task<IActionResult> GetReasonCodes([FromQuery] ReportingQueryRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await queryService.GetReasonCodeReportAsync(request, currentUser.TenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/reports/parking/allocation-outcomes.csv")]
    public async Task<IActionResult> GetAllocationOutcomesCsv([FromQuery] ReportingQueryRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var csv = await queryService.GetAllocationOutcomesCsvAsync(request, currentUser.TenantId, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "fps-allocation-outcomes.csv");
    }

    [HttpGet("/reports/parking/employee-impact")]
    public async Task<IActionResult> GetEmployeeImpact([FromQuery] FairnessQueryRequest request, [FromQuery] int minRejections = 2, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await queryService.GetEmployeeImpactAsync(request, currentUser.TenantId, minRejections, cancellationToken);
        return Ok(result);
    }
}

internal static class ReportingRoles
{
    internal const string HrManager = "hr_manager";
    internal const string Admin = "admin";
    internal const string ReportViewer = "report_viewer";
}
