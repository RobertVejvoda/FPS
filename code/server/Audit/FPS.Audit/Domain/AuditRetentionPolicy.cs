namespace FPS.Audit.Domain;

public sealed record AuditRetentionPolicy(string TenantId, int RetentionDays)
{
    public static int DefaultRetentionDays => 365 * 3;

    public DateTime CutoffUtc(DateTime now) => now.AddDays(-RetentionDays).Date;
}
