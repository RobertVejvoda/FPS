using FPS.Reporting.Domain;
using System.Security.Cryptography;
using System.Text;

namespace FPS.Reporting.Application;

public sealed class BookingEventReportingHandler(IReportingRepository repository)
{
    public async Task HandleAsync(BookingEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (await repository.EventExistsAsync(envelope.EventId, cancellationToken))
            return;

        await repository.RecordEventIdAsync(envelope.EventId, cancellationToken);

        var payload = envelope.Payload;
        var tenantId = envelope.TenantId;
        var date = payload.Date ?? envelope.OccurredAt.ToString("yyyy-MM-dd");
        var locationId = payload.LocationId ?? string.Empty;
        var timeSlot = payload.TimeSlot ?? string.Empty;

        switch (envelope.EventType)
        {
            case "booking.requestSubmitted":
                await repository.ApplyMetricsAsync(tenantId, date, locationId, timeSlot, m => m.IncrementDemand(), cancellationToken);
                if (!string.IsNullOrEmpty(payload.RequestorId))
                    await repository.ApplyFairnessAsync(tenantId, Hash(payload.RequestorId), f => f.IncrementRequest(), cancellationToken);
                break;

            case "booking.slotAllocated":
                await repository.ApplyMetricsAsync(tenantId, date, locationId, timeSlot, m => m.IncrementAllocation(), cancellationToken);
                if (!string.IsNullOrEmpty(payload.RequestorId))
                    await repository.ApplyFairnessAsync(tenantId, Hash(payload.RequestorId), f => f.IncrementAllocation(), cancellationToken);
                break;

            case "booking.requestRejected":
                await repository.ApplyMetricsAsync(tenantId, date, locationId, timeSlot, m => m.IncrementRejection(payload.ReasonCode), cancellationToken);
                break;

            case "booking.requestCancelled":
                await repository.ApplyMetricsAsync(tenantId, date, locationId, timeSlot, m => m.IncrementCancellation(), cancellationToken);
                break;

            case "booking.noShowRecorded":
                await repository.ApplyMetricsAsync(tenantId, date, locationId, timeSlot, m => m.IncrementNoShow(), cancellationToken);
                break;

            case "booking.penaltyApplied":
                await repository.ApplyMetricsAsync(tenantId, date, locationId, timeSlot, m => m.IncrementPenalty(), cancellationToken);
                break;

            case "booking.usageConfirmed":
                await repository.ApplyMetricsAsync(tenantId, date, locationId, timeSlot, m => m.IncrementUsageConfirmed(), cancellationToken);
                break;

            case "booking.drawCompleted":
            case "booking.manualCorrectionApplied":
                // No per-request metric update for these event types in v1.
                break;
        }
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
