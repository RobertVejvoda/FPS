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

    public GetDrawStatusHandler(IDrawRepository drawRepository, IAvailableSlotService slotService)
    {
        ArgumentNullException.ThrowIfNull(drawRepository);
        ArgumentNullException.ThrowIfNull(slotService);
        this.drawRepository = drawRepository;
        this.slotService = slotService;
    }

    public async Task<DrawStatusResult> Handle(GetDrawStatusQuery query, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(query.TimeSlotStart, query.TimeSlotEnd);
        var drawKey = DrawKey.Create(query.TenantId, query.LocationId, query.Date, timeSlot);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var attemptTask = drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);
        var slotsTask = slotService.GetAvailableSlotsAsync(query.TenantId, query.LocationId, query.Date, timeSlot, cancellationToken);
        await Task.WhenAll(attemptTask, slotsTask);
        var attempt = attemptTask.Result;
        var availableSpotCount = slotsTask.Result.Count;

        var (canRequest, cannotRequestReason) = ResolveCanRequest(query.Date, attempt?.Status, today);

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

    // Company-car overflow rejections have a specific reason message set by the DrawService.
    private static bool IsCompanyCarRequest(DrawDecisionDto d)
        => d.Reason?.Contains("company-car", StringComparison.OrdinalIgnoreCase) == true
        || d.Reason?.Contains("Company-car", StringComparison.OrdinalIgnoreCase) == true;
}
