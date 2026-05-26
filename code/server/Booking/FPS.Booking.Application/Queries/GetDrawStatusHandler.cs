using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetDrawStatusHandler : IRequestHandler<GetDrawStatusQuery, DrawStatusResult?>
{
    private readonly IDrawRepository drawRepository;
    private readonly IAvailableSlotService availableSlotService;
    private readonly ITenantPolicyService tenantPolicyService;
    private readonly IBookingRepository bookingRepository;

    public GetDrawStatusHandler(
        IDrawRepository drawRepository,
        IAvailableSlotService availableSlotService,
        ITenantPolicyService tenantPolicyService,
        IBookingRepository bookingRepository)
    {
        ArgumentNullException.ThrowIfNull(drawRepository);
        ArgumentNullException.ThrowIfNull(availableSlotService);
        ArgumentNullException.ThrowIfNull(tenantPolicyService);
        ArgumentNullException.ThrowIfNull(bookingRepository);
        this.drawRepository = drawRepository;
        this.availableSlotService = availableSlotService;
        this.tenantPolicyService = tenantPolicyService;
        this.bookingRepository = bookingRepository;
    }

    public async Task<DrawStatusResult?> Handle(GetDrawStatusQuery query, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(query.TimeSlotStart, query.TimeSlotEnd);
        var drawKey = DrawKey.Create(query.TenantId, query.LocationId, query.Date, timeSlot);

        // Try to get existing draw attempt
        var attempt = await drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);

        // Get policy for cutoff time and canRequest logic
        var policy = await tenantPolicyService.GetEffectivePolicyAsync(query.TenantId, query.LocationId, cancellationToken);

        // Get available slots count from Configuration service
        var availableSlots = await availableSlotService.GetAvailableSlotsAsync(
            query.TenantId,
            query.LocationId,
            query.Date,
            timeSlot,
            cancellationToken);
        int availableSpotCount = availableSlots.Count;

        // Compute nextDrawAt (the day before requested date at cutoff time)
        DateTime? nextDrawAt = null;
        if (query.Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            var drawDate = query.Date.AddDays(-1);
            var drawDateTime = new DateTime(drawDate.Year, drawDate.Month, drawDate.Day,
                policy.DrawCutOffTime.Hour, policy.DrawCutOffTime.Minute, 0, DateTimeKind.Unspecified);

            // Convert to UTC if needed (simplified - assumes TimeZoneId handling elsewhere)
            nextDrawAt = drawDateTime;
        }

        // Compute canRequest and cannotRequestReason
        var (canRequest, cannotRequestReason) = await ComputeCanRequestAsync(
            query.TenantId, query.LocationId, query.Date, policy, availableSpotCount, cancellationToken);

        // If draw exists, use its data; otherwise compute from current state
        if (attempt is not null)
        {
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
                NextDrawAt: nextDrawAt,
                CanRequest: canRequest,
                CannotRequestReason: cannotRequestReason);
        }

        // No draw yet - return pre-draw state
        // Count pending requests for this date/location (Note: CountRequestsForDateAsync doesn't filter by location)
        var requestedDateTime = new DateTime(query.Date.Year, query.Date.Month, query.Date.Day);
        int pendingRequestCount = await bookingRepository.CountRequestsForDateAsync(
            query.TenantId, requestedDateTime, cancellationToken);

        return new DrawStatusResult(
            DrawKey: drawKey.ToStoreKey(),
            TenantId: query.TenantId,
            LocationId: query.LocationId,
            Date: query.Date,
            Status: "Scheduled",
            RequestCount: pendingRequestCount,
            AllocatedCount: 0,
            RejectedCount: 0,
            WaitlistedCount: 0,
            CompanyCarOverflowCount: 0,
            SummaryRejectionReasons: new List<string>(),
            AlgorithmVersion: "N/A",
            Seed: 0,
            AuditReference: null,
            StartedAt: null,
            CompletedAt: null,
            DemandLevel: DemandLevel.Unknown,
            AvailableSpotCount: availableSpotCount,
            NextDrawAt: nextDrawAt,
            CanRequest: canRequest,
            CannotRequestReason: cannotRequestReason);
    }

    private async Task<(bool canRequest, string? cannotRequestReason)> ComputeCanRequestAsync(
        string tenantId,
        string locationId,
        DateOnly requestedDate,
        TenantPolicy policy,
        int availableSpotCount,
        CancellationToken cancellationToken)
    {
        // Check if date is in the past
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (requestedDate < today)
        {
            return (false, "Cannot request parking for past dates.");
        }

        // Check if same-day booking is allowed
        if (requestedDate == today && !policy.SameDayBookingEnabled)
        {
            return (false, "Same-day parking requests are not available for this location.");
        }

        // Check if cutoff has passed (simplified - assumes draw is day before at cutoff time)
        if (requestedDate == today.AddDays(1))
        {
            var now = DateTime.UtcNow;
            var cutoffToday = new DateTime(today.Year, today.Month, today.Day,
                policy.DrawCutOffTime.Hour, policy.DrawCutOffTime.Minute, 0, DateTimeKind.Utc);

            if (now > cutoffToday)
            {
                return (false, "The request deadline has passed for this date.");
            }
        }

        // Check if location has any available spots
        if (availableSpotCount == 0)
        {
            return (false, "No parking spaces are available for this date.");
        }

        // All checks passed
        return (true, null);
    }

    // Company-car overflow rejections have a specific reason message set by the DrawService.
    private static bool IsCompanyCarRequest(DrawDecisionDto d)
        => d.Reason?.Contains("company-car", StringComparison.OrdinalIgnoreCase) == true
        || d.Reason?.Contains("Company-car", StringComparison.OrdinalIgnoreCase) == true;
}
