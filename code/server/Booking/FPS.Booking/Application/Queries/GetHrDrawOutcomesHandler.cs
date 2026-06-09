using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetHrDrawOutcomesHandler(
    IBookingQueryRepository queryRepository,
    IDrawRepository drawRepository)
    : IRequestHandler<GetHrDrawOutcomesQuery, IReadOnlyList<HrDrawOutcomeSummary>>
{
    private static readonly IReadOnlySet<string> TerminalStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Allocated", "Rejected", "Cancelled", "NoShow", "Expired" };

    public async Task<IReadOnlyList<HrDrawOutcomeSummary>> Handle(
        GetHrDrawOutcomesQuery query, CancellationToken cancellationToken)
    {
        // Pull terminal-status bookings in the requested date range for the tenant.
        // Page size is capped; for draw history we fetch a large window and group.
        var result = await queryRepository.GetByTenantAsync(
            query.TenantId,
            query.LocationId,
            query.From,
            query.To,
            statusFilter: null,
            pageSize: 500,
            cursor: null,
            cancellationToken);

        // Group by the (date, locationId, timeSlotStart) tuple — that is the draw identity.
        var groups = result.Items
            .Where(b => TerminalStatuses.Contains(b.Status))
            .GroupBy(b => new DrawGroupKey(
                b.RequestedDate,
                b.LocationId ?? string.Empty,
                b.TimeSlotStart))
            .OrderByDescending(g => g.Key.Date)
            .ThenBy(g => g.Key.LocationId)
            .ToList();

        var summaries = new List<HrDrawOutcomeSummary>(groups.Count);
        foreach (var group in groups)
        {
            var key = group.Key;
            var drawKey = $"draw:{query.TenantId}:{key.LocationId}:{key.Date:yyyy-MM-dd}:{key.SlotStart:HHmm}";
            var drawAttempt = await drawRepository.GetByKeyAsync(drawKey, cancellationToken);

            var items = group.Select(b => new HrDrawOutcomeItem(
                b.RequestId,
                b.RequestorRef,
                b.Status,
                b.ReasonCode,
                b.Reason,
                b.AllocatedSlotId)).ToList();

            var allocated = items.Count(i => i.Outcome == "Allocated");
            var rejected = items.Count(i => i.Outcome == "Rejected");

            summaries.Add(new HrDrawOutcomeSummary(
                Date: key.Date.ToString("yyyy-MM-dd"),
                TimeSlot: $"{key.SlotStart:HH:mm}-{group.First().TimeSlotEnd:HH:mm}",
                LocationId: key.LocationId,
                DrawStatus: drawAttempt?.Status ?? (allocated + rejected > 0 ? "Completed" : "Unknown"),
                AllocatedCount: drawAttempt?.AllocatedCount ?? allocated,
                RejectedCount: drawAttempt?.RejectedCount ?? rejected,
                WaitlistedCount: drawAttempt?.WaitlistedCount ?? 0,
                TotalRequests: items.Count,
                CompletedAt: drawAttempt?.CompletedAt,
                Outcomes: items));
        }

        return summaries;
    }

    private readonly record struct DrawGroupKey(DateOnly Date, string LocationId, TimeOnly SlotStart);
}
