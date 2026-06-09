namespace FPS.Booking.Application.Workflows;

public sealed record DrawWorkflowOutput(
    string DrawKey,
    string Status,      // "Completed" | "Failed"
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount,
    string? ErrorMessage);
