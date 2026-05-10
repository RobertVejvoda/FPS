using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Infrastructure.Repositories;

public sealed class DaprBookingQueryRepository : IBookingQueryRepository
{
    private readonly DaprClient daprClient;
    private const string BookingStore = "bookingstore";

    public DaprBookingQueryRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<BookingListResult> GetByRequestorAsync(
        string tenantId,
        string requestorId,
        DateOnly from,
        DateOnly? to,
        string? statusFilter,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        var index = await daprClient.GetStateAsync<UserRequestIndex>(
            BookingStore,
            UserIndexKey(tenantId, requestorId),
            cancellationToken: cancellationToken);

        var requestIds = index?.RequestIds ?? [];

        var dtos = new List<BookingRequestDto>(requestIds.Count);
        foreach (var id in requestIds)
        {
            var dto = await daprClient.GetStateAsync<BookingRequestDto>(
                BookingStore, $"request:{id}", cancellationToken: cancellationToken);
            if (dto is not null)
                dtos.Add(dto);
        }

        var filtered = dtos
            .Where(d => DateOnly.FromDateTime(d.PlannedArrivalTime) >= from)
            .Where(d => to is null || DateOnly.FromDateTime(d.PlannedArrivalTime) <= to.Value)
            .Where(d => statusFilter is null || d.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.PlannedArrivalTime.Date)
            .ThenByDescending(d => d.RequestedAt)
            .ToList();

        var offset = DecodeCursor(cursor);
        var page = filtered.Skip(offset).Take(pageSize).ToList();
        var nextCursor = offset + page.Count < filtered.Count
            ? EncodeCursor(offset + page.Count)
            : null;

        return new BookingListResult(
            page.Select(ToListItem).ToList(),
            nextCursor);
    }

    public async Task<IReadOnlyList<BookingRequestDto>> GetPendingRequestsForDrawAsync(
        string tenantId,
        string locationId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        // Fetch tenant-wide pending index — maintained by SubmitBookingRequestHandler
        var index = await daprClient.GetStateAsync<TenantPendingIndex>(
            BookingStore,
            $"pending:{tenantId}",
            cancellationToken: cancellationToken);

        if (index is null) return [];

        var results = new List<BookingRequestDto>();
        foreach (var id in index.RequestIds)
        {
            var dto = await daprClient.GetStateAsync<BookingRequestDto>(
                BookingStore, $"request:{id}", cancellationToken: cancellationToken);

            if (dto is null || dto.Status != "Pending") continue;
            if (dto.LocationId != locationId) continue;
            if (DateOnly.FromDateTime(dto.PlannedArrivalTime) != date) continue;

            results.Add(dto);
        }

        return results;
    }

    public async Task AddToUserIndexAsync(
        string tenantId,
        string requestorId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var key = UserIndexKey(tenantId, requestorId);
        var index = await daprClient.GetStateAsync<UserRequestIndex>(
            BookingStore, key, cancellationToken: cancellationToken)
            ?? new UserRequestIndex();

        if (!index.RequestIds.Contains(requestId))
        {
            index.RequestIds.Add(requestId);
            await daprClient.SaveStateAsync(BookingStore, key, index, cancellationToken: cancellationToken);
        }
    }

    private static string UserIndexKey(string tenantId, string requestorId)
        => $"user-requests:{tenantId}:{requestorId}";

    private static BookingListItem ToListItem(BookingRequestDto dto) => new(
        RequestId: dto.RequestId,
        RequestedDate: DateOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotStart: TimeOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotEnd: TimeOnly.FromDateTime(dto.PlannedDepartureTime),
        LocationId: dto.LocationId,
        Status: dto.Status,
        Reason: ReasonFor(dto),
        AllocatedSlotId: dto.AllocatedSlotId?.ToString(),
        NextAction: NextActionFor(dto.Status),
        CreatedAt: dto.RequestedAt,
        LastStatusChangedAt: dto.LastStatusChangedAt == default ? dto.RequestedAt : dto.LastStatusChangedAt);

    private static string? ReasonFor(BookingRequestDto dto) =>
        dto.Status switch
        {
            "Rejected" => dto.RejectionReason,
            "Cancelled" => dto.CancellationReason,
            "NoShow" or "Expired" => dto.CancellationReason ?? dto.RejectionReason,
            _ => null
        };

    // Only advertise actions implemented in merged slices (B001 + B003).
    // cancel: Pending requests only. Allocated-cancel waits for B005.
    private static string NextActionFor(string status) =>
        status == "Pending" ? "cancel" : "none";

    private static int DecodeCursor(string? cursor)
    {
        if (cursor is null) return 0;
        try { return int.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor))); }
        catch { return 0; }
    }

    private static string EncodeCursor(int offset)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()));

    private sealed class UserRequestIndex
    {
        public List<Guid> RequestIds { get; set; } = [];
    }

    private sealed class TenantPendingIndex
    {
        public List<Guid> RequestIds { get; set; } = [];
    }
}
