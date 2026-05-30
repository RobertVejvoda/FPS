using FPS.Booking.Application.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FPS.Booking.Application.Services;

public sealed class DrawSchedulerService(
    DrawSchedulerOptions options,
    IMediator mediator,
    ILogger<DrawSchedulerService> logger) : IDrawSchedulerService
{
    public async Task<IReadOnlyList<DrawSchedulerResult>> TriggerDueDrawsAsync(
        DateOnly targetDate, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("DrawScheduler is disabled; skipping {Count} target(s)", options.Targets.Count);
            return options.Targets.Select(t => new DrawSchedulerResult(
                t.TenantId, t.LocationId, targetDate, DrawKey: "", Status: "Disabled")).ToList();
        }

        if (options.Targets.Count == 0)
        {
            logger.LogWarning("DrawScheduler is enabled but no targets are configured");
            return [];
        }

        var results = new List<DrawSchedulerResult>(options.Targets.Count);

        foreach (var target in options.Targets)
        {
            var slotStart = targetDate.ToDateTime(TimeOnly.FromTimeSpan(target.TimeSlotStart), DateTimeKind.Utc);
            var slotEnd   = targetDate.ToDateTime(TimeOnly.FromTimeSpan(target.TimeSlotEnd),   DateTimeKind.Utc);

            try
            {
                var result = await mediator.Send(new TriggerDrawCommand(
                    TenantId: target.TenantId,
                    LocationId: target.LocationId,
                    Date: targetDate,
                    TimeSlotStart: slotStart,
                    TimeSlotEnd: slotEnd,
                    Reason: $"Scheduled draw for {targetDate:yyyy-MM-dd}",
                    TriggerSource: "scheduled",
                    TriggeredBy: "dapr-cron"),
                    cancellationToken);

                var status = result.WasAlreadyCompleted ? "AlreadyCompleted" : result.Status;

                logger.LogInformation(
                    "Scheduled draw: tenant={TenantId} location={LocationId} date={Date} slot={SlotStart}-{SlotEnd} status={Status} key={DrawKey}",
                    target.TenantId, target.LocationId, targetDate,
                    slotStart.ToString("HH:mm"), slotEnd.ToString("HH:mm"),
                    status, result.DrawAttemptId);

                results.Add(new DrawSchedulerResult(
                    target.TenantId, target.LocationId, targetDate, result.DrawAttemptId, status));
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Scheduled draw failed unexpectedly: tenant={TenantId} location={LocationId} date={Date}",
                    target.TenantId, target.LocationId, targetDate);

                results.Add(new DrawSchedulerResult(
                    target.TenantId, target.LocationId, targetDate, DrawKey: "", Status: "Failed"));
            }
        }

        return results;
    }
}
