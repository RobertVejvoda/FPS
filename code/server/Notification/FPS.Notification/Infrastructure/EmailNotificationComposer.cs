using System.Globalization;
using System.Net;
using System.Text;
using FPS.Notification.Application;
using FPS.Notification.Domain;

namespace FPS.Notification.Infrastructure;

/// <summary>
/// NOTIF #727 — composes event-specific, business-safe email content from a stored notification
/// record. One reusable inline-CSS layout wraps a per-type subject/heading/status plus the record's
/// message and available details (date, time slot, location) and a next-action hint. Every dynamic
/// value is HTML-encoded; a plain-text twin is produced alongside the HTML. No allocation internals,
/// GUIDs, seeds, weights, stack traces, secrets, or internal URLs are ever rendered.
/// </summary>
public sealed class EmailNotificationComposer : IEmailNotificationComposer
{
    private const string BrandName = "FairSpot";
    private const string HrSuffix = ".hr";

    private sealed record EmailTemplate(string Subject, string Heading, string Status);

    // Event-specific subjects/headings — never a generic subject. HR fan-out variants (".hr") and the
    // sales alert get their own entries; unknown types fall back to a safe generic template.
    private static readonly IReadOnlyDictionary<string, EmailTemplate> Templates =
        new Dictionary<string, EmailTemplate>(StringComparer.Ordinal)
        {
            ["booking.requestSubmitted"] = new("Your parking request was submitted", "Parking request submitted", "Submitted"),
            ["booking.requestSubmitted.hr"] = new("New parking request submitted", "New parking request", "Submitted"),
            ["booking.requestRejected"] = new("Your parking request could not be allocated", "Parking request not allocated", "Not allocated"),
            ["booking.slotAllocated"] = new("Your parking spot is confirmed", "Parking spot allocated", "Allocated"),
            // Variant: a cancellation freed a spot that was reallocated to this requestor.
            ["booking.slotAllocated.reallocation"] = new("A parking spot was reallocated to you", "Parking spot reallocated", "Reallocated"),
            ["booking.requestCancelled"] = new("Your parking request was cancelled", "Parking request cancelled", "Cancelled"),
            ["booking.requestCancelled.hr"] = new("A parking request was cancelled", "Parking request cancelled", "Cancelled"),
            // Variant: an already-allocated reservation was cancelled (vs cancelled before allocation).
            ["booking.requestCancelled.postAllocation"] = new("Your allocated parking reservation was cancelled", "Parking reservation cancelled", "Cancelled"),
            ["booking.drawCompleted"] = new("Your parking allocation results", "Parking allocation complete", "Draw complete"),
            ["booking.drawCompleted.hr"] = new("Parking draw completed", "Parking draw completed", "Draw complete"),
            ["booking.noShowRecorded"] = new("Parking no-show recorded", "No-show recorded", "No-show"),
            ["booking.penaltyApplied"] = new("A parking penalty was applied", "Parking penalty applied", "Penalty applied"),
            // Variants: distinct penalty reasons get their own copy.
            ["booking.penaltyApplied.LateCancel"] = new("A late-cancellation penalty was applied", "Late-cancellation penalty", "Penalty applied"),
            ["booking.penaltyApplied.NoShow"] = new("A no-show penalty was applied", "No-show penalty", "Penalty applied"),
            ["booking.usageConfirmed"] = new("Parking usage confirmed", "Parking usage confirmed", "Confirmed"),
            ["booking.requestExpired"] = new("Your parking request expired", "Parking request expired", "Expired"),
            ["booking.manualCorrectionApplied"] = new("Your parking request was updated", "Parking request updated", "Updated"),
            ["tenant-request.received"] = new("New FairSpot pilot request", "New pilot request", "New lead"),
        };

    private static readonly EmailTemplate Fallback = new("FairSpot notification", "FairSpot notification", "Update");

    public ComposedEmail Compose(NotificationRecord record)
    {
        // Prefer a variant-specific template (keyed on safe outcome differentiators), then the base
        // NotificationType, then a safe generic fallback.
        var refinedKey = ResolveVariantKey(record);
        var template =
            (refinedKey is not null && Templates.TryGetValue(refinedKey, out var v)) ? v
            : Templates.TryGetValue(record.NotificationType, out var t) ? t
            : Fallback;
        var details = BuildDetails(record);
        var nextAction = ResolveNextAction(record.NextAction);
        var footer = ResolveFooter(record.NotificationType);

        return new ComposedEmail(
            template.Subject,
            RenderHtml(template, record.MessageText, details, nextAction, footer),
            RenderText(template, record.MessageText, details, nextAction, footer));
    }

    // Maps shared NotificationTypes to a variant key using only business-safe differentiators. Returns
    // null when the base template applies. Unrecognised values fall through to the base template.
    private static string? ResolveVariantKey(NotificationRecord record) => record.NotificationType switch
    {
        "booking.slotAllocated" when IsReallocation(record.AllocationSource) => "booking.slotAllocated.reallocation",
        "booking.requestCancelled" when IsAllocatedStatus(record.PreviousStatus) => "booking.requestCancelled.postAllocation",
        "booking.penaltyApplied" when record.ReasonCode is "LateCancel" or "NoShow" => $"booking.penaltyApplied.{record.ReasonCode}",
        _ => null,
    };

    private static bool IsReallocation(string? source) =>
        string.Equals(source, "reallocation", StringComparison.OrdinalIgnoreCase);

    // The cancelled reservation was already allocated (vs cancelled before allocation). Booking emits
    // PreviousStatus null on cancellations today, so this variant stays dormant until that field is
    // populated upstream — the template and wiring are ready for it.
    private static bool IsAllocatedStatus(string? previousStatus) =>
        string.Equals(previousStatus, "Allocated", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(previousStatus, "Reallocated", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<(string Label, string Value)> BuildDetails(NotificationRecord record)
    {
        var rows = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(record.RelatedDate))
            rows.Add(("Date", FormatDate(record.RelatedDate!)));
        if (!string.IsNullOrWhiteSpace(record.RelatedTimeSlot))
            rows.Add(("Time slot", record.RelatedTimeSlot!));
        if (!string.IsNullOrWhiteSpace(record.LocationId))
            rows.Add(("Location", record.LocationId!));
        return rows;
    }

    private static string FormatDate(string date) =>
        DateOnly.TryParse(date, CultureInfo.InvariantCulture, out var d)
            ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            : date;

    private static string? ResolveNextAction(string? nextAction) => nextAction switch
    {
        "confirmUsage" => "Please confirm your usage in FairSpot once you have parked.",
        "cancel" => "You can cancel this request in FairSpot if your plans change.",
        _ => null,
    };

    private static string ResolveFooter(string notificationType)
    {
        if (notificationType.EndsWith(HrSuffix, StringComparison.Ordinal))
            return "You received this as an HR or facilities contact for your organisation's FairSpot workspace.";
        if (notificationType == "tenant-request.received")
            return "You received this because it was sent to the FairSpot sales inbox.";
        return "You received this because it affects your FairSpot parking request.";
    }

    // ── HTML rendering (inline CSS, table layout, email-client safe) ────────────────
    private static string RenderHtml(
        EmailTemplate template, string message, IReadOnlyList<(string Label, string Value)> details,
        string? nextAction, string footer)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"margin:0;padding:0;background-color:#f4f5f7;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:#f4f5f7;\"><tr><td align=\"center\" style=\"padding:24px 12px;\">");
        sb.Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;width:100%;background-color:#ffffff;border-radius:8px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;\">");

        // Brand header
        sb.Append("<tr><td style=\"background-color:#1f2937;padding:16px 24px;\">");
        sb.Append($"<span style=\"color:#ffffff;font-size:18px;font-weight:bold;letter-spacing:0.5px;\">{Encode(BrandName)}</span>");
        sb.Append("</td></tr>");

        // Title + status
        sb.Append("<tr><td style=\"padding:24px 24px 8px 24px;\">");
        sb.Append($"<h1 style=\"margin:0;font-size:20px;color:#111827;\">{Encode(template.Heading)}</h1>");
        sb.Append($"<p style=\"margin:8px 0 0 0;font-size:13px;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;\">{Encode(template.Status)}</p>");
        sb.Append("</td></tr>");

        // Message
        sb.Append("<tr><td style=\"padding:8px 24px 0 24px;\">");
        sb.Append($"<p style=\"margin:0;font-size:15px;line-height:1.5;color:#374151;\">{EncodeMultiline(message)}</p>");
        sb.Append("</td></tr>");

        // Detail rows
        if (details.Count > 0)
        {
            sb.Append("<tr><td style=\"padding:16px 24px 0 24px;\">");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:#f9fafb;border-radius:6px;\">");
            foreach (var (label, value) in details)
            {
                sb.Append("<tr>");
                sb.Append($"<td style=\"padding:8px 12px;font-size:13px;color:#6b7280;width:110px;\">{Encode(label)}</td>");
                sb.Append($"<td style=\"padding:8px 12px;font-size:13px;color:#111827;font-weight:bold;\">{Encode(value)}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></td></tr>");
        }

        // Next action
        if (nextAction is not null)
        {
            sb.Append("<tr><td style=\"padding:16px 24px 0 24px;\">");
            sb.Append($"<p style=\"margin:0;font-size:14px;line-height:1.5;color:#1f2937;\"><strong>Next step:</strong> {Encode(nextAction)}</p>");
            sb.Append("</td></tr>");
        }

        // Footer
        sb.Append("<tr><td style=\"padding:24px;\">");
        sb.Append("<hr style=\"border:none;border-top:1px solid #e5e7eb;margin:0 0 12px 0;\">");
        sb.Append($"<p style=\"margin:0;font-size:12px;line-height:1.5;color:#9ca3af;\">{Encode(footer)}</p>");
        sb.Append("</td></tr>");

        sb.Append("</table></td></tr></table></div>");
        return sb.ToString();
    }

    // ── Plain-text rendering ────────────────────────────────────────────────────────
    private static string RenderText(
        EmailTemplate template, string message, IReadOnlyList<(string Label, string Value)> details,
        string? nextAction, string footer)
    {
        var sb = new StringBuilder();
        sb.Append(BrandName).Append('\n');
        sb.Append(template.Heading).Append(" (").Append(template.Status).Append(")\n\n");
        sb.Append(message.Trim()).Append('\n');
        if (details.Count > 0)
        {
            sb.Append('\n');
            foreach (var (label, value) in details)
                sb.Append(label).Append(": ").Append(value).Append('\n');
        }
        if (nextAction is not null)
            sb.Append("\nNext step: ").Append(nextAction).Append('\n');
        sb.Append("\n—\n").Append(footer).Append('\n');
        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);

    // Encode then turn newlines into <br> so multi-line messages keep their breaks without
    // allowing raw HTML through (encoding happens before the <br> substitution).
    private static string EncodeMultiline(string value) =>
        Encode(value)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
}
