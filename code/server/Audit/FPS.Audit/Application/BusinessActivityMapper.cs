namespace FPS.Audit.Application;

public static class BusinessActivityMapper
{
    private static readonly IReadOnlyDictionary<string, string> ResultMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["booking.requestSubmitted"]        = "accepted",
            ["booking.requestRejected"]         = "rejected",
            ["booking.slotAllocated"]           = "allocated",
            ["booking.requestCancelled"]        = "cancelled",
            ["booking.drawStarted"]             = "started",
            ["booking.drawCompleted"]           = "completed",
            ["booking.drawFailed"]              = "failed",
            ["booking.noShowRecorded"]          = "recorded",
            ["booking.penaltyApplied"]          = "applied",
            ["booking.manualCorrectionApplied"] = "updated",
            ["booking.usageConfirmed"]          = "confirmed",
            ["booking.requestExpired"]          = "expired",
            ["privacy.erasureRequested"]        = "accepted",
            ["privacy.erasureCompleted"]        = "completed",
            ["privacy.erasureRejected"]         = "rejected",
            ["privacy.erasureStepRecorded"]     = "recorded",
        };

    public static string ToResult(string eventType) =>
        ResultMap.TryGetValue(eventType, out var r) ? r : "recorded";

    public static string ToSummary(string action, string entityType, string? result, string? reasonCode)
    {
        var base_ = $"{action} on {entityType}";
        if (!string.IsNullOrEmpty(result))
            base_ += $" — {result}";
        if (!string.IsNullOrEmpty(reasonCode))
            base_ += $" ({reasonCode})";
        return base_;
    }
}
