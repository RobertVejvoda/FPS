using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Infrastructure.Repositories;

public sealed class DaprBookingRepository : IBookingRepository
{
    private readonly DaprClient daprClient;
    private const string BookingStore = "bookingstore";

    public DaprBookingRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task CreateBookingRequestAsync(BookingRequestDto request)
    {
        request.LastStatusChangedAt = DateTime.UtcNow;
        await daprClient.SaveStateAsync(BookingStore, $"request:{request.RequestId}", request);
    }

    public async Task<BookingRequestDto?> GetBookingRequestAsync(Guid requestId)
        => await daprClient.GetStateAsync<BookingRequestDto>(BookingStore, $"request:{requestId}");

    public async Task UpdateBookingRequestStatusAsync(
        Guid requestId, string status, string? reason = null, CancellationToken cancellationToken = default)
    {
        var dto = await GetBookingRequestAsync(requestId);
        if (dto is null) return;

        dto.Status = status;
        dto.LastStatusChangedAt = DateTime.UtcNow;

        if (status == "Cancelled") dto.CancellationReason = reason;
        else if (status == "Rejected") dto.RejectionReason = reason;

        await daprClient.SaveStateAsync(BookingStore, $"request:{requestId}", dto, cancellationToken: cancellationToken);
    }

    public async Task UpdateBookingRequestUsageAsync(
        Guid requestId, string confirmationSource, DateTime confirmedAt,
        string? sourceEventId = null, CancellationToken cancellationToken = default)
    {
        var dto = await GetBookingRequestAsync(requestId);
        if (dto is null) return;

        dto.Status = "Used";
        dto.ConfirmationSource = confirmationSource;
        dto.UsageConfirmedAt = confirmedAt;
        dto.ConfirmationSourceEventId = sourceEventId;
        dto.LastStatusChangedAt = DateTime.UtcNow;

        await daprClient.SaveStateAsync(BookingStore, $"request:{requestId}", dto, cancellationToken: cancellationToken);
    }

    public async Task<int> CountRequestsForDateAsync(
        string tenantId, DateTime date, CancellationToken cancellationToken = default)
    {
        var counter = await daprClient.GetStateAsync<RequestDateCounter>(
            BookingStore, $"count:{tenantId}:{date:yyyy-MM-dd}", cancellationToken: cancellationToken);
        return counter?.Count ?? 0;
    }

    public async Task<bool> HasOverlappingRequestAsync(
        string tenantId, string requestorId, TimeSlot period, CancellationToken cancellationToken = default)
    {
        // Requires MongoDB driver range query — stub until infrastructure test phase.
        await Task.CompletedTask;
        return false;
    }

    public async Task CreateAllocationAsync(AllocationDto allocation)
    {
        await daprClient.SaveStateAsync(BookingStore, $"allocation:{allocation.AllocationId}", allocation);

        await daprClient.SaveStateAsync(
            BookingStore,
            $"facility:{allocation.FacilityId}:allocation:{allocation.AllocationId}",
            new FacilityAllocationIndex
            {
                FacilityId = allocation.FacilityId,
                AllocationId = allocation.AllocationId,
                SlotId = allocation.SlotId,
                StartTime = allocation.StartTime,
                EndTime = allocation.EndTime
            });

        await daprClient.SaveStateAsync(
            BookingStore,
            $"status:{allocation.Status}:allocation:{allocation.AllocationId}",
            new StatusAllocationIndex { Status = allocation.Status, AllocationId = allocation.AllocationId });
    }

    public async Task<AllocationDto?> GetAllocationAsync(Guid allocationId)
        => await daprClient.GetStateAsync<AllocationDto>(BookingStore, $"allocation:{allocationId}");

    public async Task<IEnumerable<AllocationDto>> GetAllocationsByStatusAsync(string status)
    {
        var indexes = await daprClient.GetStateAsync<Dictionary<string, StatusAllocationIndex>>(
            BookingStore, $"status:{status}");
        if (indexes is null) return [];

        var results = new List<AllocationDto>();
        foreach (var index in indexes.Values)
        {
            var dto = await GetAllocationAsync(index.AllocationId);
            if (dto is not null) results.Add(dto);
        }
        return results;
    }

    public async Task<IEnumerable<AllocationDto>> GetAllocationsByFacilityAsync(
        Guid facilityId, DateTime from, DateTime to)
    {
        var indexes = await daprClient.GetStateAsync<Dictionary<string, FacilityAllocationIndex>>(
            BookingStore, $"facility:{facilityId}");
        if (indexes is null) return [];

        var results = new List<AllocationDto>();
        foreach (var index in indexes.Values.Where(i => i.StartTime <= to && i.EndTime >= from))
        {
            var dto = await GetAllocationAsync(index.AllocationId);
            if (dto is not null) results.Add(dto);
        }
        return results;
    }

    public async Task UpdateAllocationStatusAsync(Guid allocationId, string status, string? reason = null)
    {
        var dto = await GetAllocationAsync(allocationId);
        if (dto is null) return;

        await daprClient.DeleteStateAsync(BookingStore, $"status:{dto.Status}:allocation:{allocationId}");

        dto.Status = status;
        dto.StatusReason = reason;
        dto.LastUpdated = DateTime.UtcNow;

        await daprClient.SaveStateAsync(BookingStore, $"allocation:{allocationId}", dto);
        await daprClient.SaveStateAsync(
            BookingStore, $"status:{status}:allocation:{allocationId}",
            new StatusAllocationIndex { Status = status, AllocationId = allocationId });
    }

    public async Task UpdateAllocationArrivalAsync(Guid allocationId, DateTime arrivalTime, string confirmedBy)
    {
        var dto = await GetAllocationAsync(allocationId);
        if (dto is null) return;

        dto.Status = SlotAllocationStatus.InUse.ToString();
        dto.ActualArrivalTime = arrivalTime;
        dto.ArrivalConfirmedBy = confirmedBy;
        dto.LastUpdated = DateTime.UtcNow;

        await daprClient.SaveStateAsync(BookingStore, $"allocation:{allocationId}", dto);
    }

    public async Task UpdateAllocationDepartureAsync(Guid allocationId, DateTime departureTime, string confirmedBy)
    {
        var dto = await GetAllocationAsync(allocationId);
        if (dto is null) return;

        dto.Status = SlotAllocationStatus.Completed.ToString();
        dto.ActualDepartureTime = departureTime;
        dto.DepartureConfirmedBy = confirmedBy;
        dto.LastUpdated = DateTime.UtcNow;

        await daprClient.SaveStateAsync(BookingStore, $"allocation:{allocationId}", dto);
        await daprClient.SaveStateAsync(
            BookingStore, $"status:Completed:allocation:{allocationId}",
            new StatusAllocationIndex { Status = "Completed", AllocationId = allocationId });
    }

    private sealed class StatusAllocationIndex
    {
        public string Status { get; set; } = string.Empty;
        public Guid AllocationId { get; set; }
    }

    private sealed class FacilityAllocationIndex
    {
        public Guid FacilityId { get; set; }
        public Guid AllocationId { get; set; }
        public Guid SlotId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    private sealed class RequestDateCounter
    {
        public int Count { get; set; }
    }
}
