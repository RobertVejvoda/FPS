using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetMyDrawOutcomesHandler(
    IBookingQueryRepository queryRepository,
    IDrawRepository drawRepository)
    : IRequestHandler<GetMyDrawOutcomesQuery, IReadOnlyList<MyDrawOutcomeSummary>>
{
    private static readonly IReadOnlySet<string> TerminalStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Allocated", "Rejected", "Cancelled", "NoShow", "Expired" };

    public async Task<IReadOnlyList<MyDrawOutcomeSummary>> Handle(
        GetMyDrawOutcomesQuery query, CancellationToken cancellationToken)
    {
        var result = await queryRepository.GetByRequestorAsync(
            query.TenantId,
            query.RequestorId,
            query.From,
            query.To,
            statusFilter: null,
            pageSize: 500,
            cursor: null,
            cancellationToken);

        // Group by draw identity — each draw should have at most one entry per employee.
        var groups = result.Items
            .Where(b => TerminalStatuses.Contains(b.Status))
            .GroupBy(b => new DrawGroupKey(b.RequestedDate, b.LocationId ?? string.Empty, b.TimeSlotStart))
            .OrderByDescending(g => g.Key.Date)
            .ThenBy(g => g.Key.LocationId)
            .ToList();

        var summaries = new List<MyDrawOutcomeSummary>(groups.Count);
        foreach (var group in groups)
        {
            var key = group.Key;
            var drawKey = $"draw:{query.TenantId}:{key.LocationId}:{key.Date:yyyy-MM-dd}:{key.SlotStart:HHmm}";
            var drawAttempt = await drawRepository.GetByKeyAsync(drawKey, cancellationToken);

            // Take the most recent terminal booking for this employee in this draw slot.
            var booking = group.OrderByDescending(b => b.LastStatusChangedAt).First();

            var fallbackAllocated = group.Count(b => b.Status == "Allocated");
            var totalRequests = group.Count();

            summaries.Add(new MyDrawOutcomeSummary(
                Date: key.Date.ToString("yyyy-MM-dd"),
                TimeSlot: $"{key.SlotStart:HH:mm}-{group.First().TimeSlotEnd:HH:mm}",
                LocationId: key.LocationId,
                DrawStatus: drawAttempt?.Status ?? (totalRequests > 0 ? "Completed" : "Unknown"),
                AllocatedCount: drawAttempt?.AllocatedCount ?? fallbackAllocated,
                TotalRequests: drawAttempt != null
                    ? drawAttempt.AllocatedCount + drawAttempt.RejectedCount + drawAttempt.WaitlistedCount
                    : totalRequests,
                CompletedAt: drawAttempt?.CompletedAt,
                MyOutcome: booking.Status,
                MyReason: booking.Reason,
                MyAllocatedSlotId: booking.AllocatedSlotId));
        }

        return summaries;
    }

    private readonly record struct DrawGroupKey(DateOnly Date, string LocationId, TimeOnly SlotStart);
}
