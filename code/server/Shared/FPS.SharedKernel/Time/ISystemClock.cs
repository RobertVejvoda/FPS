namespace FPS.SharedKernel.Time;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
