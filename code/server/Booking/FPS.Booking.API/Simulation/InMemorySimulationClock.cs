using FPS.SharedKernel.Time;

namespace FPS.Booking.API.Simulation;

public sealed class InMemorySimulationClock : ISystemClock
{
    private readonly object _lock = new();
    private TimeSpan? _offset;

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_lock)
                return _offset.HasValue ? DateTimeOffset.UtcNow + _offset.Value : DateTimeOffset.UtcNow;
        }
    }

    public bool IsSimulating
    {
        get { lock (_lock) { return _offset.HasValue && _offset.Value != TimeSpan.Zero; } }
    }

    public void Advance(TimeSpan delta)
    {
        lock (_lock)
            _offset = (_offset ?? TimeSpan.Zero) + delta;
    }

    public void Reset()
    {
        lock (_lock)
            _offset = null;
    }
}
