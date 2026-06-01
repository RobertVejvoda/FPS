using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Application;

public sealed class EventInboxService(
    DataHubDbContext db,
    IEnumerable<IProjectionHandler> handlers)
{
    private const int MaxRetries = 3;

    public async Task AcceptAsync(BookingEventEnvelope envelope, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(envelope.EventId) || string.IsNullOrEmpty(envelope.TenantId))
            return;

        var existing = await db.EventInbox
            .FirstOrDefaultAsync(e => e.SourceEventId == envelope.EventId, ct);

        if (existing is not null)
        {
            if (existing.ProcessingStatus is EventProcessingStatus.Processed or EventProcessingStatus.Poisoned)
                return;
            if (existing.RetryCount >= MaxRetries)
            {
                existing.ProcessingStatus = EventProcessingStatus.Poisoned;
                await db.SaveChangesAsync(ct);
                return;
            }
            await DispatchAsync(existing, envelope, ct);
            return;
        }

        var payloadJson = JsonSerializer.Serialize(envelope.Payload);
        var record = new EventInboxRecord
        {
            SourceEventId = envelope.EventId,
            EventName = envelope.EventType,
            EventVersion = envelope.EventVersion,
            TenantId = envelope.TenantId,
            SourceService = envelope.Source,
            AggregateId = envelope.Payload.BookingRequestId ?? envelope.Payload.AllocationId,
            OccurredAt = new DateTimeOffset(envelope.OccurredAt, TimeSpan.Zero),
            PublishedAt = envelope.PublishedAt.HasValue
                ? new DateTimeOffset(envelope.PublishedAt.Value, TimeSpan.Zero)
                : null,
            Payload = payloadJson,
            PayloadHash = ComputeHash(payloadJson),
            ProcessingStatus = EventProcessingStatus.Pending,
        };

        db.EventInbox.Add(record);
        await db.SaveChangesAsync(ct);

        await DispatchAsync(record, envelope, ct);
    }

    private async Task DispatchAsync(EventInboxRecord record, BookingEventEnvelope envelope, CancellationToken ct)
    {
        var applicable = handlers.Where(h => h.CanHandle(envelope.EventType)).ToList();
        if (applicable.Count == 0)
            return; // No handlers registered yet — leave Pending so future handlers can process it

        try
        {
            foreach (var h in applicable)
                await h.HandleAsync(envelope, ct);

            record.ProcessedAt = DateTimeOffset.UtcNow;
            record.ProcessingStatus = EventProcessingStatus.Processed;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            record.RetryCount++;
            record.ProcessingError = Truncate(ex.Message, 500);
            record.ProcessingStatus = record.RetryCount >= MaxRetries
                ? EventProcessingStatus.Poisoned
                : EventProcessingStatus.Failed;
            await db.SaveChangesAsync(ct);

            if (record.ProcessingStatus == EventProcessingStatus.Failed)
                throw; // Return 500 so Dapr retries delivery
            // Poisoned: accept delivery as terminal, no rethrow
        }
    }

    private static string ComputeHash(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..16];
    }

    private static string Truncate(string? message, int max) =>
        message is null ? string.Empty : message.Length <= max ? message : message[..max];
}
