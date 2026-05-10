namespace FPS.Booking.Application.Models;

public class CorrectionAuditDto
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string CorrectionType { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
}
