using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FPS.DataHub.Application;

/// <summary>
/// Projection handler for booking Draw and request lifecycle events.
/// Updates DrawHistoryProjection and BookingOutcomeProjection idempotently.
/// </summary>
public sealed class BookingProjectionHandler(
    DataHubDbContext db,
    ILogger<BookingProjectionHandler> logger) : IProjectionHandler
{
    public bool CanHandle(string eventType) => eventType switch
    {
        "booking.drawStarted" => true,
        "booking.drawCompleted" => true,
        "booking.requestSubmitted" => true,
        "booking.requestAllocated" or "booking.slotAllocated" => true,
        "booking.requestRejected" => true,
        "booking.requestCancelled" => true,
        "booking.usageConfirmed" => true,
        "booking.noShowRecorded" => true,
        "booking.requestExpired" => true,
        _ => false
    };

    public async Task HandleAsync(BookingEventEnvelope envelope, CancellationToken ct)
    {
        logger.LogInformation("Handling {EventType} for tenant {TenantId}", envelope.EventType, envelope.TenantId);

        switch (envelope.EventType)
        {
            case "booking.drawStarted":
                await HandleDrawStarted(envelope, ct);
                break;
            case "booking.drawCompleted":
                await HandleDrawCompleted(envelope, ct);
                break;
            case "booking.requestSubmitted":
                await HandleRequestSubmitted(envelope, ct);
                break;
            case "booking.slotAllocated":
            case "booking.requestAllocated":
                await HandleRequestAllocated(envelope, ct);
                break;
            case "booking.requestRejected":
                await HandleRequestRejected(envelope, ct);
                break;
            case "booking.requestCancelled":
                await HandleRequestCancelled(envelope, ct);
                break;
            case "booking.usageConfirmed":
                await HandleUsageConfirmed(envelope, ct);
                break;
            case "booking.noShowRecorded":
                await HandleNoShowRecorded(envelope, ct);
                break;
            case "booking.requestExpired":
                await HandleRequestExpired(envelope, ct);
                break;
        }
    }

    private async Task HandleDrawStarted(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.LocationId) || string.IsNullOrEmpty(payload.Date) || string.IsNullOrEmpty(payload.TimeSlot))
        {
            logger.LogWarning("DrawStarted event missing required fields: {EventId}", envelope.EventId);
            return;
        }

        var drawAttemptId = payload.DrawAttemptId ?? envelope.EventId;
        var existing = await db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == drawAttemptId, ct);

        if (existing is null)
        {
            var projection = new DrawHistoryProjection
            {
                DrawAttemptId = drawAttemptId,
                TenantId = envelope.TenantId,
                LocationId = payload.LocationId,
                Date = DateOnly.Parse(payload.Date),
                TimeSlot = payload.TimeSlot,
                Status = "Running",
                // Booking publishes the trigger source on payload.ReasonCode
                // and the HR-supplied reason on payload.ReasonText (#472).
                // Fall back to the legacy actor-derived value for old
                // events that pre-date the schema change.
                TriggerSource = payload.ReasonCode ?? (envelope.ActorType == "system" ? "scheduled" : "manual"),
                RunReason = payload.ReasonText,
                TriggeredBy = envelope.ActorType == "system" ? null : envelope.ActorId,
                StartedAt = envelope.OccurredAt,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            db.DrawHistory.Add(projection);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created DrawHistory projection for {DrawAttemptId}", drawAttemptId);
        }
        else
        {
            logger.LogDebug("DrawHistory projection already exists for {DrawAttemptId}, skipping duplicate", drawAttemptId);
        }
    }

    private async Task HandleDrawCompleted(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.LocationId) || string.IsNullOrEmpty(payload.Date) || string.IsNullOrEmpty(payload.TimeSlot))
        {
            logger.LogWarning("DrawCompleted event missing required fields: {EventId}", envelope.EventId);
            return;
        }

        var drawAttemptId = payload.DrawAttemptId ?? envelope.CausationId ?? envelope.EventId;

        var projection = await db.DrawHistory.FirstOrDefaultAsync(
            d => d.DrawAttemptId == drawAttemptId, ct);

        if (projection is null)
        {
            // If no started event was processed, create projection from completed event
            projection = new DrawHistoryProjection
            {
                DrawAttemptId = drawAttemptId,
                TenantId = envelope.TenantId,
                LocationId = payload.LocationId,
                Date = DateOnly.Parse(payload.Date),
                TimeSlot = payload.TimeSlot,
                Status = "Completed",
                TriggerSource = payload.ReasonCode ?? (envelope.ActorType == "system" ? "scheduled" : "manual"),
                RunReason = payload.ReasonText,
                TriggeredBy = envelope.ActorType == "system" ? null : envelope.ActorId,
                StartedAt = envelope.OccurredAt.AddSeconds(-1), // Approximate
                CompletedAt = envelope.OccurredAt,
                AllocatedCount = payload.AllocatedCount ?? 0,
                RejectedCount = payload.RejectedCount ?? 0,
                WaitlistedCount = payload.WaitlistedCount ?? 0,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            db.DrawHistory.Add(projection);
            logger.LogInformation("Created DrawHistory projection from completed event for {DrawAttemptId}", drawAttemptId);
        }
        else
        {
            projection.Status = "Completed";
            projection.CompletedAt = envelope.OccurredAt;
            projection.AllocatedCount = payload.AllocatedCount ?? 0;
            projection.RejectedCount = payload.RejectedCount ?? 0;
            projection.WaitlistedCount = payload.WaitlistedCount ?? 0;
            // Backfill metadata on update — drawStarted may have missed it
            // if Booking deployed before this schema change. Never overwrite
            // an existing value with null. The completed event's explicit
            // ReasonCode wins over the started event's actor-derived
            // fallback, since the completed payload is the more
            // authoritative source for what actually happened.
            projection.RunReason ??= payload.ReasonText;
            projection.TriggeredBy ??= envelope.ActorType == "system" ? null : envelope.ActorId;
            if (payload.ReasonCode is not null)
                projection.TriggerSource = payload.ReasonCode;
            projection.LastUpdatedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Updated DrawHistory projection for {DrawAttemptId}", drawAttemptId);
        }

        await LinkUndecidedOutcomesToCompletedDraw(payload, envelope, drawAttemptId, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task LinkUndecidedOutcomesToCompletedDraw(
        BookingEventPayload payload,
        BookingEventEnvelope envelope,
        string drawAttemptId,
        CancellationToken ct)
    {
        var date = DateOnly.Parse(payload.Date!);
        var locationId = payload.LocationId!;
        var timeSlot = payload.TimeSlot!;

        var undecided = await db.BookingOutcomes
            .Where(b => b.TenantId == envelope.TenantId
                        && (b.LocationId == locationId || b.LocationId == "")
                        && b.Date == date
                        && b.TimeSlot == timeSlot
                        && b.DrawAttemptId == null
                        && b.FinalStatus == "Submitted")
            .Where(b => b.SubmittedAt == null || b.SubmittedAt <= envelope.OccurredAt)
            .ToListAsync(ct);

        foreach (var outcome in undecided)
        {
            outcome.DrawAttemptId = drawAttemptId;
            outcome.LocationId = locationId;
            outcome.FinalStatus = "Waitlisted";
            outcome.DecidedAt = envelope.OccurredAt;
            outcome.LastUpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task HandleRequestSubmitted(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.BookingRequestId) || string.IsNullOrEmpty(payload.RequestorId))
        {
            logger.LogWarning("RequestSubmitted event missing required fields: {EventId}", envelope.EventId);
            return;
        }

        var existing = await db.BookingOutcomes.FirstOrDefaultAsync(
            b => b.BookingRequestId == payload.BookingRequestId, ct);

        if (existing is null)
        {
            var projection = new BookingOutcomeProjection
            {
                BookingRequestId = payload.BookingRequestId,
                TenantId = envelope.TenantId,
                RequestorId = payload.RequestorId,
                LocationId = payload.LocationId ?? "",
                Date = DateOnly.Parse(payload.Date ?? DateOnly.FromDateTime(envelope.OccurredAt).ToString("yyyy-MM-dd")),
                TimeSlot = payload.TimeSlot ?? "",
                FinalStatus = "Submitted",
                SubmittedAt = envelope.OccurredAt,
                DecidedAt = envelope.OccurredAt,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            db.BookingOutcomes.Add(projection);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created BookingOutcome projection for {RequestId}", payload.BookingRequestId);
        }
        else
        {
            logger.LogDebug("BookingOutcome projection already exists for {RequestId}, skipping duplicate", payload.BookingRequestId);
        }
    }

    private async Task HandleRequestAllocated(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.BookingRequestId))
        {
            logger.LogWarning("RequestAllocated event missing BookingRequestId: {EventId}", envelope.EventId);
            return;
        }

        var projection = await db.BookingOutcomes.FirstOrDefaultAsync(
            b => b.BookingRequestId == payload.BookingRequestId, ct);

        if (projection is null)
        {
            // Create projection if it doesn't exist (e.g., same-day allocation without prior submission event)
            projection = new BookingOutcomeProjection
            {
                BookingRequestId = payload.BookingRequestId,
                TenantId = envelope.TenantId,
                RequestorId = payload.RequestorId ?? "",
                LocationId = payload.LocationId ?? "",
                Date = DateOnly.Parse(payload.Date ?? DateOnly.FromDateTime(envelope.OccurredAt).ToString("yyyy-MM-dd")),
                TimeSlot = payload.TimeSlot ?? "",
                FinalStatus = "Allocated",
                AllocationId = payload.AllocationId,
                SlotId = payload.SlotId,
                AllocationSource = payload.AllocationSource,
                DrawAttemptId = payload.DrawAttemptId,
                SubmittedAt = envelope.OccurredAt,
                DecidedAt = envelope.OccurredAt,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            db.BookingOutcomes.Add(projection);
            logger.LogInformation("Created BookingOutcome projection with Allocated status for {RequestId}", payload.BookingRequestId);
        }
        else
        {
            projection.FinalStatus = "Allocated";
            projection.AllocationId = payload.AllocationId;
            projection.SlotId = payload.SlotId;
            projection.AllocationSource = payload.AllocationSource;
            projection.DrawAttemptId = payload.DrawAttemptId;
            projection.DecidedAt = envelope.OccurredAt;
            projection.LastUpdatedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Updated BookingOutcome projection to Allocated for {RequestId}", payload.BookingRequestId);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task HandleRequestRejected(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.BookingRequestId))
        {
            logger.LogWarning("RequestRejected event missing BookingRequestId: {EventId}", envelope.EventId);
            return;
        }

        var projection = await db.BookingOutcomes.FirstOrDefaultAsync(
            b => b.BookingRequestId == payload.BookingRequestId, ct);

        if (projection is null)
        {
            // Create projection if it doesn't exist
            projection = new BookingOutcomeProjection
            {
                BookingRequestId = payload.BookingRequestId,
                TenantId = envelope.TenantId,
                RequestorId = payload.RequestorId ?? "",
                LocationId = payload.LocationId ?? "",
                Date = DateOnly.Parse(payload.Date ?? DateOnly.FromDateTime(envelope.OccurredAt).ToString("yyyy-MM-dd")),
                TimeSlot = payload.TimeSlot ?? "",
                FinalStatus = "Rejected",
                ReasonCode = payload.ReasonCode,
                SafeReasonText = payload.ReasonText,
                SubmittedAt = envelope.OccurredAt,
                DecidedAt = envelope.OccurredAt,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            db.BookingOutcomes.Add(projection);
            logger.LogInformation("Created BookingOutcome projection with Rejected status for {RequestId}", payload.BookingRequestId);
        }
        else
        {
            projection.FinalStatus = "Rejected";
            projection.ReasonCode = payload.ReasonCode;
            projection.SafeReasonText = payload.ReasonText;
            projection.DecidedAt = envelope.OccurredAt;
            projection.LastUpdatedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Updated BookingOutcome projection to Rejected for {RequestId}", payload.BookingRequestId);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task HandleRequestCancelled(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.BookingRequestId))
        {
            logger.LogWarning("RequestCancelled event missing BookingRequestId: {EventId}", envelope.EventId);
            return;
        }

        var projection = await db.BookingOutcomes.FirstOrDefaultAsync(
            b => b.BookingRequestId == payload.BookingRequestId, ct);

        if (projection is not null)
        {
            projection.FinalStatus = "Cancelled";
            projection.ReasonCode = payload.ReasonCode;
            projection.SafeReasonText = payload.ReasonText;
            projection.DecidedAt = envelope.OccurredAt;
            projection.LastUpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Updated BookingOutcome projection to Cancelled for {RequestId}", payload.BookingRequestId);
        }
    }

    private async Task HandleUsageConfirmed(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.BookingRequestId))
        {
            logger.LogWarning("UsageConfirmed event missing BookingRequestId: {EventId}", envelope.EventId);
            return;
        }

        var projection = await db.BookingOutcomes.FirstOrDefaultAsync(
            b => b.BookingRequestId == payload.BookingRequestId, ct);

        if (projection is not null)
        {
            projection.FinalStatus = "Used";
            projection.DecidedAt = envelope.OccurredAt;
            projection.LastUpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Updated BookingOutcome projection to Used for {RequestId}", payload.BookingRequestId);
        }
    }

    private async Task HandleNoShowRecorded(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.BookingRequestId))
        {
            logger.LogWarning("NoShowRecorded event missing BookingRequestId: {EventId}", envelope.EventId);
            return;
        }

        var projection = await db.BookingOutcomes.FirstOrDefaultAsync(
            b => b.BookingRequestId == payload.BookingRequestId, ct);

        if (projection is not null)
        {
            projection.FinalStatus = "NoShow";
            projection.DecidedAt = envelope.OccurredAt;
            projection.LastUpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Updated BookingOutcome projection to NoShow for {RequestId}", payload.BookingRequestId);
        }
    }

    private async Task HandleRequestExpired(BookingEventEnvelope envelope, CancellationToken ct)
    {
        var payload = envelope.Payload;
        if (string.IsNullOrEmpty(payload.BookingRequestId))
        {
            logger.LogWarning("RequestExpired event missing BookingRequestId: {EventId}", envelope.EventId);
            return;
        }

        var projection = await db.BookingOutcomes.FirstOrDefaultAsync(
            b => b.BookingRequestId == payload.BookingRequestId, ct);

        if (projection is not null)
        {
            projection.FinalStatus = "Expired";
            projection.ReasonCode = payload.ReasonCode;
            projection.SafeReasonText = payload.ReasonText;
            projection.DecidedAt = envelope.OccurredAt;
            projection.LastUpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Updated BookingOutcome projection to Expired for {RequestId}", payload.BookingRequestId);
        }
    }
}
