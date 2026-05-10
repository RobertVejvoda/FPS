using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.ValueObjects;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetDrawStatusHandler : IRequestHandler<GetDrawStatusQuery, DrawStatusResult?>
{
    private readonly IDrawRepository drawRepository;

    public GetDrawStatusHandler(IDrawRepository drawRepository)
    {
        ArgumentNullException.ThrowIfNull(drawRepository);
        this.drawRepository = drawRepository;
    }

    public async Task<DrawStatusResult?> Handle(GetDrawStatusQuery query, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(query.TimeSlotStart, query.TimeSlotEnd);
        var drawKey = DrawKey.Create(query.TenantId, query.LocationId, query.Date, timeSlot);

        var attempt = await drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);
        if (attempt is null) return null;

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
            CompletedAt: attempt.CompletedAt);
    }

    // Company-car overflow rejections have a specific reason message set by the DrawService.
    private static bool IsCompanyCarRequest(DrawDecisionDto d)
        => d.Reason?.Contains("company-car", StringComparison.OrdinalIgnoreCase) == true
        || d.Reason?.Contains("Company-car", StringComparison.OrdinalIgnoreCase) == true;
}
