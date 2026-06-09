using System.Collections.Concurrent;
using FPS.SharedKernel.Time;

namespace FPS.Booking.Simulation;

public sealed class InMemorySimulationClock : ISystemClock
{
    private readonly ConcurrentDictionary<string, TimeSpan> _tenantOffsets = new();

    /// <summary>Real UTC — always returns system time; used for audit timestamps and the Dapr scheduler.</summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <summary>Returns virtual time for the tenant if a simulation offset has been set; otherwise real UTC.</summary>
    public DateTimeOffset GetTenantUtcNow(string tenantId)
    {
        return _tenantOffsets.TryGetValue(tenantId, out var offset)
            ? DateTimeOffset.UtcNow + offset
            : DateTimeOffset.UtcNow;
    }

    public bool IsTenantSimulating(string tenantId) => _tenantOffsets.ContainsKey(tenantId);

    public void Advance(string tenantId, TimeSpan delta)
    {
        _tenantOffsets.AddOrUpdate(tenantId, delta, (_, existing) => existing + delta);
    }

    public void Reset(string tenantId)
    {
        _tenantOffsets.TryRemove(tenantId, out _);
    }

    public DateTimeOffset? GetVirtualNow(string tenantId)
    {
        return _tenantOffsets.TryGetValue(tenantId, out var offset)
            ? DateTimeOffset.UtcNow + offset
            : null;
    }
}
