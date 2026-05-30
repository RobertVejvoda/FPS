using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetDrawStatusHandler : IRequestHandler<GetDrawStatusQuery, DrawStatusResult>
{
    private readonly IDrawRepository drawRepository;
    private readonly IAvailableSlotService slotService;
    private readonly ITenantPolicyService policyService;

    public GetDrawStatusHandler(
        IDrawRepository drawRepository,
        IAvailableSlotService slotService,
        ITenantPolicyService policyService)
    {
        ArgumentNullException.ThrowIfNull(drawRepository);
        ArgumentNullException.ThrowIfNull(slotService);
        ArgumentNullException.ThrowIfNull(policyService);
        this.drawRepository = drawRepository;
        this.slotService = slotService;
        this.policyService = policyService;
    }

    public async Task<DrawStatusResult> Handle(GetDrawStatusQuery query, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(query.TimeSlotStart, query.TimeSlotEnd);
        var drawKey = DrawKey.Create(query.TenantId, query.LocationId, query.Date, timeSlot);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var attemptTask = drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);
        var slotsTask = slotService.GetAvailableSlotsAsync(query.TenantId, query.LocationId, query.Date, timeSlot, cancellationToken);
        var policyTask = policyService.GetEffectivePolicyAsync(query.TenantId, query.LocationId, cancellationToken);
        await Task.WhenAll(attemptTask, slotsTask, policyTask);
        var attempt = attemptTask.Result;
        var availableSpotCount = slotsTask.Result.Count;
        var policy = policyTask.Result;

        var (canRequest, cannotRequestReason) = ResolveCanRequest(query.Date, attempt?.Status, today);
        var schedule = BuildScheduleMetadata(policy, query.Date, attempt?.Status, today);

        if (attempt is null)
        {
            return new DrawStatusResult(
                DrawKey: drawKey.ToStoreKey(),
                TenantId: query.TenantId,
                LocationId: query.LocationId,
                Date: query.Date,
                Status: "NotScheduled",
                RequestCount: 0,
                AllocatedCount: 0,
                RejectedCount: 0,
                WaitlistedCount: 0,
                CompanyCarOverflowCount: 0,
                SummaryRejectionReasons: [],
                AlgorithmVersion: string.Empty,
                Seed: 0,
                AuditReference: null,
                StartedAt: null,
                CompletedAt: null,
                DemandLevel: DemandLevel.Unknown,
                CutOffAt: schedule.CutOffAt,
                NextDrawAt: schedule.NextDrawAt,
                TimeZone: schedule.TimeZone,
                RequestWindowStatus: schedule.RequestWindowStatus,
                ScheduleStatus: schedule.ScheduleStatus,
                ScheduleSource: schedule.ScheduleSource,
                LastCalculatedAt: schedule.LastCalculatedAt,
                SafeMessage: schedule.SafeMessage,
                AvailableSpotCount: availableSpotCount,
                CanRequest: canRequest,
                CannotRequestReason: cannotRequestReason);
        }

        var companyCarOverflowCount = attempt.Decisions
            .Count(d => d.Outcome == "Rejected" && IsCompanyCarRequest(d));

        var summaryRejectionReasons = attempt.Decisions
            .Where(d => d.Outcome == "Rejected" && !string.IsNullOrEmpty(d.Reason))
            .Select(d => d.Reason!)
            .Distinct()
            .ToList();

        return new DrawStatusResult(
            DrawKey: attempt.DrawKey,
            TenantId: attempt.TenantId,
            LocationId: attempt.LocationId,
            Date: attempt.Date,
            Status: attempt.Status,
            RequestCount: attempt.Decisions.Count,
            AllocatedCount: attempt.AllocatedCount,
            RejectedCount: attempt.RejectedCount,
            WaitlistedCount: attempt.WaitlistedCount,
            CompanyCarOverflowCount: companyCarOverflowCount,
            SummaryRejectionReasons: summaryRejectionReasons,
            AlgorithmVersion: attempt.AlgorithmVersion,
            Seed: attempt.Seed,
            AuditReference: attempt.DrawKey,
            StartedAt: attempt.StartedAt,
            CompletedAt: attempt.CompletedAt,
            DemandLevel: attempt.Status == "Completed"
                ? DemandLevel.FromOutcomes(attempt.Decisions.Count, attempt.AllocatedCount)
                : DemandLevel.Unknown,
            CutOffAt: schedule.CutOffAt,
            NextDrawAt: schedule.NextDrawAt,
            TimeZone: schedule.TimeZone,
            RequestWindowStatus: schedule.RequestWindowStatus,
            ScheduleStatus: schedule.ScheduleStatus,
            ScheduleSource: schedule.ScheduleSource,
            LastCalculatedAt: schedule.LastCalculatedAt,
            SafeMessage: schedule.SafeMessage,
            AvailableSpotCount: availableSpotCount,
            CanRequest: canRequest,
            CannotRequestReason: cannotRequestReason);
    }

    private static (bool CanRequest, string? CannotRequestReason) ResolveCanRequest(
        DateOnly date, string? drawStatus, DateOnly today)
    {
        if (date < today)
            return (false, "Date has passed");
        if (drawStatus is "Completed")
            return (false, "Spot allocation is complete for this date");
        if (drawStatus is "InProgress")
            return (false, "Draw in progress");
        return (true, null);
    }

    private static ScheduleMeta BuildScheduleMetadata(
        TenantPolicy? policy,
        DateOnly date,
        string? drawStatus,
        DateOnly today)
    {
        var calculatedAt = DateTime.UtcNow;

        if (policy is null)
        {
            return new ScheduleMeta(
                CutOffAt: null,
                NextDrawAt: null,
                TimeZone: "UTC",
                RequestWindowStatus: Models.RequestWindowStatus.Unknown,
                ScheduleStatus: Models.ScheduleStatus.NotConfigured,
                ScheduleSource: Models.ScheduleSource.TenantPolicy,
                LastCalculatedAt: calculatedAt,
                SafeMessage: "Allocation schedule is not yet configured for this location.");
        }

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId); }
        catch { tz = TimeZoneInfo.Utc; }

        var cutOffDay = date.AddDays(-1);
        var localCutOff = cutOffDay.ToDateTime(policy.DrawCutOffTime);
        var offset = tz.GetUtcOffset(localCutOff);
        var cutOffDto = new DateTimeOffset(localCutOff, offset);
        var cutOffAt = cutOffDto.ToString("O");

        var now = DateTimeOffset.UtcNow;
        var windowClosed = date < today
            || drawStatus is "Completed" or "InProgress" or "Failed"
            || now >= cutOffDto;

        var windowStatus = windowClosed
            ? Models.RequestWindowStatus.Closed
            : Models.RequestWindowStatus.Open;

        var safeMessage = drawStatus is "Completed"
            ? "Spot allocation is complete. Check your result in My Spots."
            : windowClosed
                ? "The request window is closed for this date."
                : $"Requests are open until {policy.DrawCutOffTime:HH:mm} ({policy.TimeZoneId}).";

        return new ScheduleMeta(
            CutOffAt: cutOffAt,
            NextDrawAt: cutOffAt,
            TimeZone: policy.TimeZoneId,
            RequestWindowStatus: windowStatus,
            ScheduleStatus: Models.ScheduleStatus.Known,
            ScheduleSource: Models.ScheduleSource.TenantPolicy,
            LastCalculatedAt: calculatedAt,
            SafeMessage: safeMessage);
    }

    private static bool IsCompanyCarRequest(DrawDecisionDto d)
        => d.Reason?.Contains("company-car", StringComparison.OrdinalIgnoreCase) == true
        || d.Reason?.Contains("Company-car", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record ScheduleMeta(
        string? CutOffAt,
        string? NextDrawAt,
        string TimeZone,
        string RequestWindowStatus,
        string ScheduleStatus,
        string ScheduleSource,
        DateTime LastCalculatedAt,
        string SafeMessage);
}
