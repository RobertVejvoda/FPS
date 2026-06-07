namespace FPS.SharedKernel.Time;

public interface ISystemClock
{
    /// <summary>Real UTC time — use for audit timestamps and global scheduler ticks.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Virtual UTC time for a specific tenant — may be ahead of real time during simulation.</summary>
    DateTimeOffset GetTenantUtcNow(string tenantId);
}
