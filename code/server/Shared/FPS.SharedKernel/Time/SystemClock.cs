namespace FPS.SharedKernel.Time;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTimeOffset GetTenantUtcNow(string tenantId) => DateTimeOffset.UtcNow;
}
