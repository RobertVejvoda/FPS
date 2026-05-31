namespace FPS.DataHub.Application;

public interface IProjectionHandler
{
    bool CanHandle(string eventType);
    Task HandleAsync(BookingEventEnvelope envelope, CancellationToken ct);
}
