namespace FPS.Booking.Application.Services;

public interface IDrawSchedulerService
{
    Task<IReadOnlyList<DrawSchedulerResult>> TriggerDueDrawsAsync(
        DateOnly targetDate, string? tenantId = null, CancellationToken cancellationToken = default);
}

public sealed record DrawSchedulerResult(
    string TenantId,
    string LocationId,
    DateOnly Date,
    string DrawKey,
    string Status); // "Started" | "AlreadyRunning" | "AlreadyCompleted" | "Failed" | "Disabled"
