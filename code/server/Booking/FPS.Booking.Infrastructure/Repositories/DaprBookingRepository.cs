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

    private sealed class RequestDateCounter
    {
        public int Count { get; set; }
    }
}
