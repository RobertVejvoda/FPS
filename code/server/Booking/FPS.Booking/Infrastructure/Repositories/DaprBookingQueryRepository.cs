using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.ValueObjects;

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
                BookingStore, TenantStorageKey.For("request", tenantId, id), cancellationToken: cancellationToken);
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
            nextCursor,
            filtered.Count);
    }

    public async Task<IReadOnlyList<BookingRequestDto>> GetAllocatedRequestsForDrawAsync(
        string tenantId, string locationId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var index = await daprClient.GetStateAsync<TenantPendingIndex>(
            BookingStore, PendingIndexKey(tenantId), cancellationToken: cancellationToken);

        if (index is null) return [];

        var results = new List<BookingRequestDto>();
        foreach (var id in index.RequestIds)
        {
            var dto = await daprClient.GetStateAsync<BookingRequestDto>(
                BookingStore, TenantStorageKey.For("request", tenantId, id), cancellationToken: cancellationToken);

            if (dto is null || dto.Status != "Allocated") continue;
            if (dto.LocationId != locationId) continue;
            if (DateOnly.FromDateTime(dto.PlannedArrivalTime) != date) continue;

            results.Add(dto);
        }

        return results;
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
            PendingIndexKey(tenantId),
            cancellationToken: cancellationToken);

        if (index is null) return [];

        var results = new List<BookingRequestDto>();
        foreach (var id in index.RequestIds)
        {
            var dto = await daprClient.GetStateAsync<BookingRequestDto>(
                BookingStore, TenantStorageKey.For("request", tenantId, id), cancellationToken: cancellationToken);

            if (dto is null || dto.Status != "Pending") continue;
            if (dto.LocationId != locationId) continue;
            if (DateOnly.FromDateTime(dto.PlannedArrivalTime) != date) continue;

            results.Add(dto);
        }

        return results;
    }

    public async Task<HrBookingListResult> GetByTenantAsync(
        string tenantId,
        string? locationId,
        DateOnly? from,
        DateOnly? to,
        string? statusFilter,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        var index = await daprClient.GetStateAsync<TenantOpsIndex>(
            BookingStore, OpsIndexKey(tenantId), cancellationToken: cancellationToken);

        var requestIds = index?.RequestIds ?? [];

        var dtos = new List<BookingRequestDto>(requestIds.Count);
        foreach (var id in requestIds)
        {
            var dto = await daprClient.GetStateAsync<BookingRequestDto>(
                BookingStore, TenantStorageKey.For("request", tenantId, id), cancellationToken: cancellationToken);
            if (dto is not null)
                dtos.Add(dto);
        }

        var filtered = dtos
            .Where(d => locationId is null || d.LocationId == locationId)
            .Where(d => from is null || DateOnly.FromDateTime(d.PlannedArrivalTime) >= from.Value)
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

        return new HrBookingListResult(
            page.Select(ToHrListItem).ToList(),
            nextCursor,
            filtered.Count);
    }

    public async Task<HrEmployeeHistoryResult> GetEmployeeHistoryAsync(
        string tenantId,
        string requestorId,
        DateOnly? from,
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
                BookingStore, TenantStorageKey.For("request", tenantId, id), cancellationToken: cancellationToken);
            if (dto is not null)
                dtos.Add(dto);
        }

        // Date-range window first — summary counts span the window but ignore
        // the status filter so HR can read the overall pattern even when
        // drilling into one status.
        var inWindow = dtos
            .Where(d => from is null || DateOnly.FromDateTime(d.PlannedArrivalTime) >= from.Value)
            .Where(d => to is null || DateOnly.FromDateTime(d.PlannedArrivalTime) <= to.Value)
            .ToList();

        var summary = new HrEmployeeHistorySummary(
            Total: inWindow.Count,
            Allocated: inWindow.Count(d => d.Status == "Allocated"),
            Rejected: inWindow.Count(d => d.Status == "Rejected"),
            Cancelled: inWindow.Count(d => d.Status is "Cancelled" or "NoShow" or "Expired"),
            Pending: inWindow.Count(d => d.Status == "Pending"));

        var filtered = inWindow
            .Where(d => statusFilter is null || MatchesStatusFilter(d.Status, statusFilter))
            .OrderByDescending(d => d.PlannedArrivalTime.Date)
            .ThenByDescending(d => d.RequestedAt)
            .ToList();

        var offset = DecodeCursor(cursor);
        var page = filtered.Skip(offset).Take(pageSize).ToList();
        var nextCursor = offset + page.Count < filtered.Count
            ? EncodeCursor(offset + page.Count)
            : null;

        return new HrEmployeeHistoryResult(
            RequestorRef: requestorId,
            Summary: summary,
            Items: page.Select(ToHrEmployeeHistoryItem).ToList(),
            NextCursor: nextCursor,
            TotalCount: filtered.Count);
    }

    public async Task<HrSlotHistoryResult> GetSlotHistoryAsync(
        string tenantId,
        string? locationId,
        string slotId,
        DateOnly? from,
        DateOnly? to,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        // Read the tenant ops index — the same source the HR Parking Requests
        // list uses — and filter to bookings allocated to this specific slot.
        // Stays HR-safe: we project to HrSlotHistoryItem only, never expose
        // lottery internals, candidate sequences, or penalty scores.
        var index = await daprClient.GetStateAsync<TenantOpsIndex>(
            BookingStore, OpsIndexKey(tenantId), cancellationToken: cancellationToken);

        var requestIds = index?.RequestIds ?? [];

        var dtos = new List<BookingRequestDto>(requestIds.Count);
        foreach (var id in requestIds)
        {
            var dto = await daprClient.GetStateAsync<BookingRequestDto>(
                BookingStore, TenantStorageKey.For("request", tenantId, id), cancellationToken: cancellationToken);
            if (dto is not null)
                dtos.Add(dto);
        }

        var filtered = dtos
            .Where(d => string.Equals(d.AllocatedSlotId, slotId, StringComparison.OrdinalIgnoreCase))
            .Where(d => locationId is null || d.LocationId == locationId)
            .Where(d => from is null || DateOnly.FromDateTime(d.PlannedArrivalTime) >= from.Value)
            .Where(d => to is null || DateOnly.FromDateTime(d.PlannedArrivalTime) <= to.Value)
            .OrderByDescending(d => d.PlannedArrivalTime.Date)
            .ThenByDescending(d => d.LastStatusChangedAt)
            .ToList();

        var offset = DecodeCursor(cursor);
        var page = filtered.Skip(offset).Take(pageSize).ToList();
        var nextCursor = offset + page.Count < filtered.Count
            ? EncodeCursor(offset + page.Count)
            : null;

        return new HrSlotHistoryResult(
            SlotId: slotId,
            Items: page.Select(ToHrSlotHistoryItem).ToList(),
            NextCursor: nextCursor,
            TotalCount: filtered.Count);
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

    public async Task AddToTenantPendingIndexAsync(
        string tenantId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var key = PendingIndexKey(tenantId);
        var index = await daprClient.GetStateAsync<TenantPendingIndex>(
            BookingStore, key, cancellationToken: cancellationToken)
            ?? new TenantPendingIndex();

        if (!index.RequestIds.Contains(requestId))
        {
            index.RequestIds.Add(requestId);
            await daprClient.SaveStateAsync(BookingStore, key, index, cancellationToken: cancellationToken);
        }
    }

    public async Task AddToTenantOpsIndexAsync(
        string tenantId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var key = OpsIndexKey(tenantId);
        var index = await daprClient.GetStateAsync<TenantOpsIndex>(
            BookingStore, key, cancellationToken: cancellationToken)
            ?? new TenantOpsIndex();

        if (!index.RequestIds.Contains(requestId))
        {
            index.RequestIds.Add(requestId);
            await daprClient.SaveStateAsync(BookingStore, key, index, cancellationToken: cancellationToken);
        }
    }

    public async Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        // The tenant ops index is the master list of every booking request for the tenant —
        // SubmitBookingRequestHandler adds every request to it — so enumerating it reaches
        // every per-request record without a query-capable store scan.
        var opsKey = OpsIndexKey(tenantId);
        var opsIndex = await daprClient.GetStateAsync<TenantOpsIndex>(
            BookingStore, opsKey, cancellationToken: cancellationToken);
        var requestIds = opsIndex?.RequestIds ?? [];

        var requestorIds = new HashSet<string>(StringComparer.Ordinal);
        var countKeys = new HashSet<string>(StringComparer.Ordinal);
        var removed = 0;

        foreach (var requestId in requestIds)
        {
            var requestKey = TenantStorageKey.For("request", tenantId, requestId);
            var dto = await daprClient.GetStateAsync<BookingRequestDto>(
                BookingStore, requestKey, cancellationToken: cancellationToken);

            if (dto is not null)
            {
                if (!string.IsNullOrEmpty(dto.RequestedBy))
                    requestorIds.Add(dto.RequestedBy);

                countKeys.Add(CountKey(tenantId, dto.PlannedArrivalTime));

                // Draw attempts are owned and purged by DaprDrawRepository.PurgeTenantAsync via the
                // per-tenant draw index, so this repository no longer reconstructs draw keys here.

                await daprClient.DeleteStateAsync(BookingStore, requestKey, cancellationToken: cancellationToken);
                removed++;
            }

            // Penalties are keyed penalty:{tenant}:{requestId}:{type} with no index, so delete
            // the key for every known penalty type (mirrors DaprPenaltyRepository.Key). Runs for
            // every id in the index — even a stale one whose request record is already gone.
            foreach (var penaltyType in Enum.GetNames<PenaltyType>())
                await daprClient.DeleteStateAsync(
                    BookingStore,
                    TenantStorageKey.For("penalty", tenantId, $"{requestId}:{penaltyType}"),
                    cancellationToken: cancellationToken);
        }

        // Per-requestor keys: the fairness metrics history (DaprEmployeeMetricsService) and the
        // user request index maintained by this repository.
        foreach (var requestorId in requestorIds)
        {
            await daprClient.DeleteStateAsync(
                BookingStore, TenantStorageKey.For("metrics", tenantId, requestorId), cancellationToken: cancellationToken);
            await daprClient.DeleteStateAsync(
                BookingStore, UserIndexKey(tenantId, requestorId), cancellationToken: cancellationToken);
        }

        // Per-date submission counters (keyed by planned arrival date, mirroring DaprBookingRepository).
        foreach (var countKey in countKeys)
            await daprClient.DeleteStateAsync(BookingStore, countKey, cancellationToken: cancellationToken);

        // Finally the tenant-wide indexes themselves. Removing the ops index last makes the purge
        // idempotent: a re-run finds no index, enumerates nothing, and returns 0.
        await daprClient.DeleteStateAsync(BookingStore, PendingIndexKey(tenantId), cancellationToken: cancellationToken);
        await daprClient.DeleteStateAsync(BookingStore, opsKey, cancellationToken: cancellationToken);

        return removed;
    }

    private static string CountKey(string tenantId, DateTime plannedArrival)
        => $"count:{TenantStorageKey.Sanitise(tenantId)}:{plannedArrival:yyyy-MM-dd}";

    private static string OpsIndexKey(string tenantId)
        => $"ops:{TenantStorageKey.Sanitise(tenantId)}";

    private static string PendingIndexKey(string tenantId)
        => $"pending:{TenantStorageKey.Sanitise(tenantId)}";

    private static string UserIndexKey(string tenantId, string requestorId)
        => $"user-requests:{TenantStorageKey.Sanitise(tenantId)}:{requestorId}";

    private static BookingListItem ToListItem(BookingRequestDto dto) => new(
        RequestId: dto.RequestId,
        RequestedDate: DateOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotStart: TimeOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotEnd: TimeOnly.FromDateTime(dto.PlannedDepartureTime),
        LocationId: dto.LocationId,
        Status: dto.Status,
        ReasonCode: dto.Status == "Rejected" ? dto.RejectionCode : null,
        Reason: ReasonFor(dto),
        AllocatedSlotId: dto.AllocatedSlotId?.ToString(),
        NextAction: NextActionFor(dto.Status),
        CreatedAt: dto.RequestedAt,
        LastStatusChangedAt: dto.LastStatusChangedAt == default ? dto.RequestedAt : dto.LastStatusChangedAt,
        ResourceType: NormalizeResourceType(dto.ResourceType));

    // Requests persisted before PLAT-seats have no resource type and are Parking by definition.
    private static string NormalizeResourceType(string? resourceType) =>
        string.IsNullOrWhiteSpace(resourceType) ? "Parking" : resourceType;

    private static HrBookingListItem ToHrListItem(BookingRequestDto dto) => new(
        RequestId: dto.RequestId,
        RequestorRef: dto.RequestedBy,
        RequestedDate: DateOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotStart: TimeOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotEnd: TimeOnly.FromDateTime(dto.PlannedDepartureTime),
        LocationId: dto.LocationId,
        Status: dto.Status,
        ReasonCode: dto.Status == "Rejected" ? dto.RejectionCode : null,
        Reason: HrReasonFor(dto),
        AllocatedSlotId: dto.AllocatedSlotId?.ToString(),
        CreatedAt: dto.RequestedAt,
        LastStatusChangedAt: dto.LastStatusChangedAt == default ? dto.RequestedAt : dto.LastStatusChangedAt,
        ResourceType: NormalizeResourceType(dto.ResourceType));

    private static HrSlotHistoryItem ToHrSlotHistoryItem(BookingRequestDto dto) => new(
        RequestId: dto.RequestId,
        RequestorRef: dto.RequestedBy,
        RequestedDate: DateOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotStart: TimeOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotEnd: TimeOnly.FromDateTime(dto.PlannedDepartureTime),
        LocationId: dto.LocationId,
        Status: dto.Status,
        ReasonCode: dto.Status == "Rejected" ? dto.RejectionCode : null,
        Reason: HrReasonFor(dto),
        AllocatedSlotId: dto.AllocatedSlotId,
        CreatedAt: dto.RequestedAt,
        LastStatusChangedAt: dto.LastStatusChangedAt == default ? dto.RequestedAt : dto.LastStatusChangedAt);

    private static HrEmployeeHistoryItem ToHrEmployeeHistoryItem(BookingRequestDto dto) => new(
        RequestId: dto.RequestId,
        RequestedDate: DateOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotStart: TimeOnly.FromDateTime(dto.PlannedArrivalTime),
        TimeSlotEnd: TimeOnly.FromDateTime(dto.PlannedDepartureTime),
        LocationId: dto.LocationId,
        Status: dto.Status,
        ReasonCode: dto.Status == "Rejected" ? dto.RejectionCode : null,
        Reason: ReasonFor(dto),
        AllocatedSlotId: dto.AllocatedSlotId?.ToString(),
        CreatedAt: dto.RequestedAt,
        LastStatusChangedAt: dto.LastStatusChangedAt == default ? dto.RequestedAt : dto.LastStatusChangedAt);

    // Group "Cancelled" filter with operational no-show/expired equivalents so
    // HR can see all "did not use" outcomes together.
    private static bool MatchesStatusFilter(string status, string filter) =>
        filter.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
            ? status is "Cancelled" or "NoShow" or "Expired"
            : status.Equals(filter, StringComparison.OrdinalIgnoreCase);

    private static string? HrReasonFor(BookingRequestDto dto) =>
        dto.Status switch
        {
            "Rejected" => dto.RejectionCode,
            "Cancelled" => "Cancelled",
            "NoShow" or "Expired" => dto.Status,
            _ => null
        };

    private static string? ReasonFor(BookingRequestDto dto) =>
        dto.Status switch
        {
            "Rejected" => dto.RejectionReason,
            "Cancelled" => dto.CancellationReason,
            "NoShow" or "Expired" => dto.CancellationReason ?? dto.RejectionReason,
            _ => null
        };

    private static string NextActionFor(string status) =>
        status switch
        {
            "Pending" => "cancel",
            "Allocated" => "confirmUsage",
            _ => "none"
        };

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

    private sealed class TenantOpsIndex
    {
        public List<Guid> RequestIds { get; set; } = [];
    }
}
